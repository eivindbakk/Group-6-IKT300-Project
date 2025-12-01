using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts;
using Microkernel.Messaging;
using Microkernel.Plugins;
using Microkernel.Services;

namespace Microkernel.Core
{
    public sealed class Kernel : IKernel, IPluginHost
    {
        private readonly KernelConfiguration _configuration;
        private readonly IPluginLoader _pluginLoader;
        private readonly IMessageBus _messageBus;
        private readonly IKernelLogger _logger;

        private readonly List<PluginContext> _plugins = new List<PluginContext>();
        private readonly ReaderWriterLockSlim _pluginsLock = new ReaderWriterLockSlim(LockRecursionPolicy. SupportsRecursion);
        private readonly object _stateLock = new object();

        private KernelState _state = KernelState.Created;

        public Kernel(
            KernelConfiguration configuration,
            IPluginLoader pluginLoader,
            IMessageBus messageBus,
            IKernelLogger logger)
        {
            _configuration = configuration ??  throw new ArgumentNullException(nameof(configuration));
            _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _configuration. Validate();
        }

        public static Kernel CreateDefault()
        {
            return CreateDefault(KernelConfiguration.CreateDefault());
        }

        public static Kernel CreateDefault(KernelConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = KernelConfiguration.CreateDefault();
            }

            var logger = new ConsoleKernelLogger();
            var messageBus = new MessageBus(logger);
            var pluginLoader = new PluginLoader(logger);

            return new Kernel(configuration, pluginLoader, messageBus, logger);
        }

        public KernelState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _state = value;
                }
            }
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (_state != KernelState. Created && _state != KernelState.Stopped)
                {
                    throw new InvalidOperationException("Cannot start kernel in state: " + _state);
                }
                _state = KernelState.Starting;
            }

            _logger.Info("Kernel starting...");

            try
            {
                LoadPlugins();
                StartPlugins();
                State = KernelState.Running;
                _logger.Info("Kernel started.  " + _plugins.Count + " plugin(s) active.");
            }
            catch (Exception ex)
            {
                _logger.Error("Kernel failed to start: " + ex.Message);
                State = KernelState.Faulted;
                throw;
            }
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                if (_state != KernelState. Running)
                {
                    return;
                }
                _state = KernelState. Stopping;
            }

            _logger. Info("Kernel stopping...");

            try
            {
                StopPlugins();
                UnloadPlugins();
                State = KernelState.Stopped;
                _logger.Info("Kernel stopped.");
            }
            catch (Exception ex)
            {
                _logger.Error("Error during kernel shutdown: " + ex.Message);
                State = KernelState.Faulted;
            }
        }

        public void Publish(EventMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (State != KernelState.Running)
            {
                return;
            }

            if (message. Timestamp == default)
            {
                message. Timestamp = DateTime. UtcNow;
            }

            _messageBus.Publish(message);
            DispatchToPlugins(message);
        }

        public IReadOnlyList<PluginInfo> GetLoadedPlugins()
        {
            _pluginsLock. EnterReadLock();
            try
            {
                var result = new List<PluginInfo>();
                foreach (var p in _plugins)
                {
                    result.Add(new PluginInfo
                    {
                        Name = p.Plugin.Name,
                        Version = p. Plugin.Version != null ? p.Plugin.Version.ToString() : "1.0.0",
                        State = p. State. ToString(),
                        LoadedAt = p. LoadedAt
                    });
                }
                return result.AsReadOnly();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets a plugin instance by name.
        /// </summary>
        public IPlugin GetPlugin(string name)
        {
            _pluginsLock. EnterReadLock();
            try
            {
                var context = _plugins.FirstOrDefault(p =>
                    p. Plugin.Name. Equals(name, StringComparison. OrdinalIgnoreCase));
                return context?.Plugin;
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }
        }

        void IPluginHost. Publish(EventMessage evt)
        {
            Publish(evt);
        }

        void IPluginHost.Log(string message)
        {
            _logger.Info("[Plugin] " + message);
        }

        private void LoadPlugins()
        {
            var searchOption = _configuration.SearchSubdirectories
                ? SearchOption. AllDirectories
                : SearchOption.TopDirectoryOnly;

            var loadedPlugins = _pluginLoader.LoadPlugins(
                _configuration. PluginsDirectory,
                _configuration.PluginSearchPattern,
                searchOption);

            _pluginsLock.EnterWriteLock();
            try
            {
                foreach (var plugin in loadedPlugins)
                {
                    var context = new PluginContext(plugin);
                    _plugins.Add(context);
                    _logger.Info("Loaded plugin: " + plugin.Name + " v" + plugin.Version);
                }
            }
            finally
            {
                _pluginsLock.ExitWriteLock();
            }
        }

        private void StartPlugins()
        {
            _pluginsLock.EnterReadLock();
            List<PluginContext> pluginsToStart;
            try
            {
                pluginsToStart = _plugins.ToList();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }

            foreach (var context in pluginsToStart)
            {
                try
                {
                    context.State = PluginState.Starting;

                    var timeoutMs = (int)_configuration. PluginStartTimeout.TotalMilliseconds;
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var startTask = Task.Run(() => context.Plugin.Start(this), cts.Token);

                        if (! startTask.Wait(timeoutMs))
                        {
                            throw new TimeoutException("Plugin " + context.Plugin. Name + " start timed out.");
                        }

                        if (startTask.Exception != null)
                        {
                            throw startTask.Exception. InnerException ??  startTask.Exception;
                        }
                    }

                    context.State = PluginState.Running;
                }
                catch (Exception ex)
                {
                    context.State = PluginState.Faulted;
                    context.LastError = ex. Message;
                    _logger.Error("Failed to start plugin " + context.Plugin. Name + ": " + ex.Message);

                    if (! _configuration.ContinueOnPluginError)
                    {
                        throw;
                    }
                }
            }
        }

        private void StopPlugins()
        {
            _pluginsLock.EnterReadLock();
            List<PluginContext> pluginsToStop;
            try
            {
                pluginsToStop = _plugins.Where(p => p. State == PluginState.Running).ToList();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }

            pluginsToStop. Reverse();

            foreach (var context in pluginsToStop)
            {
                try
                {
                    context.State = PluginState. Stopping;

                    var timeoutMs = (int)_configuration.PluginStopTimeout. TotalMilliseconds;
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var stopTask = Task. Run(() => context. Plugin.Stop(), cts.Token);
                        stopTask.Wait(timeoutMs);
                    }

                    context.State = PluginState.Stopped;
                }
                catch (Exception ex)
                {
                    context. State = PluginState.Faulted;
                    context.LastError = ex.Message;
                    _logger. Error("Error stopping plugin " + context. Plugin.Name + ": " + ex. Message);
                }
            }
        }

        private void UnloadPlugins()
        {
            _pluginsLock.EnterWriteLock();
            try
            {
                _plugins.Clear();
            }
            finally
            {
                _pluginsLock. ExitWriteLock();
            }
        }

        private void DispatchToPlugins(EventMessage message)
        {
            _pluginsLock.EnterReadLock();
            List<PluginContext> activePlugins;
            try
            {
                activePlugins = _plugins.Where(p => p.State == PluginState.Running).ToList();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }

            foreach (var context in activePlugins)
            {
                try
                {
                    context.Plugin.HandleEvent(message);
                }
                catch (Exception ex)
                {
                    _logger. Error("Plugin " + context.Plugin.Name + " error: " + ex. Message);
                }
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            if (State == KernelState.Running)
            {
                Stop();
            }

            _pluginsLock. Dispose();
            _disposed = true;
        }
    }
}