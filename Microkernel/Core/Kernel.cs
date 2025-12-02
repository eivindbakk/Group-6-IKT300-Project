using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Contracts;
using Microkernel.IPC;
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
        private readonly PluginProcessManager _processManager;

        private readonly List<PluginContext> _plugins = new List<PluginContext>();

        private readonly ReaderWriterLockSlim _pluginsLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly object _stateLock = new object();

        private KernelState _state = KernelState.Created;

        private readonly List<string> _autoSubscriptionPatterns = new List<string>();
        private IDisposable _autoMetricsSubscription;

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
            _processManager = new PluginProcessManager(logger);

            _configuration. Validate();

            // Wire up process manager events - Handle events published by external plugin processes
            _processManager.OnPluginPublish += HandleExternalPluginEvent;
        }

        private void HandleExternalPluginEvent(EventMessage evt)
        {
            if (evt == null) return;

            _logger.Debug("Received event from external plugin: " + evt.Topic);

            // Publish to message bus (for subscriptions)
            _messageBus.Publish(evt);

            // Dispatch to in-process plugins
            DispatchToPlugins(evt);

            // Broadcast to other external processes (exclude the source to prevent loops)
            string sourcePlugin = evt.Source ??  "";
            _processManager.BroadcastEventExcept(evt, sourcePlugin + "Process");
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
                if (_state != KernelState.Created && _state != KernelState.Stopped)
                {
                    throw new InvalidOperationException("Cannot start kernel in state: " + _state);
                }

                _state = KernelState.Starting;
            }

            _logger.Info("Kernel starting.. .");

            try
            {
                LoadPlugins();
                LaunchExternalPlugins();
                StartPlugins();
                State = KernelState.Running;
                _logger.Info("Kernel started.   " + _plugins.Count + " plugin(s) active.");

                TryCreateAutoMetricsSubscription();
            }
            catch (Exception ex)
            {
                _logger.Error("Kernel failed to start: " + ex.Message);
                State = KernelState.Faulted;
                throw;
            }
        }

        /// <summary>
        /// Launches plugins that run as separate OS processes (IPC via named pipes).
        /// This satisfies the assignment requirement for process isolation.
        /// </summary>
        private void LaunchExternalPlugins()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logger.Info("Looking for external plugin executables in: " + baseDir);

            // Launch MetricsLogger
            LaunchExternalPlugin("MetricsLoggerProcess", new[]
            {
                Path.Combine(baseDir, "MetricsLogger.exe"),
                Path.Combine(baseDir, "MetricsLogger"),
                Path.Combine(baseDir, ". .", "Plugins", "MetricsLoggerProcess", "bin", "Debug", "net8.0",
                    "MetricsLogger.exe"),
                Path.Combine(baseDir, "..", "Plugins", "MetricsLoggerProcess", "bin", "Release", "net8.0",
                    "MetricsLogger.exe"),
            });

            // Launch EventGenerator
            LaunchExternalPlugin("EventGeneratorProcess", new[]
            {
                Path.Combine(baseDir, "EventGenerator.exe"),
                Path.Combine(baseDir, "EventGenerator"),
                Path.Combine(baseDir, "..", "Plugins", "EventGeneratorPlugin", "bin", "Debug", "net8.0",
                    "EventGenerator.exe"),
                Path.Combine(baseDir, ". .", "Plugins", "EventGeneratorPlugin", "bin", "Release", "net8.0",
                    "EventGenerator.exe"),
            });
        }

        private void LaunchExternalPlugin(string pluginName, string[] searchPaths)
        {
            foreach (var path in searchPaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        _logger.Info("Found external plugin: " + fullPath);
                        if (_processManager.LaunchPlugin(pluginName, fullPath))
                        {
                            _logger.Info(pluginName + " launched as SEPARATE OS PROCESS (IPC via named pipes).");
                        }

                        return;
                    }
                }
                catch
                {
                    // Ignore path resolution errors
                }
            }

            _logger.Debug(pluginName + " executable not found - will use in-process plugin if available.");
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                if (_state != KernelState.Running)
                {
                    return;
                }

                _state = KernelState.Stopping;
            }

            _logger.Info("Kernel stopping...");

            try
            {
                try
                {
                    _autoMetricsSubscription?.Dispose();
                    _autoMetricsSubscription = null;
                    _autoSubscriptionPatterns.Clear();
                }
                catch
                {
                    /* ignore */
                }

                StopPlugins();
                UnloadPlugins();

                // Stop external plugin processes
                try
                {
                    _processManager?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error("Error stopping external plugin processes: " + ex.Message);
                }

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

            if (message.Timestamp == default)
            {
                message.Timestamp = DateTime.UtcNow;
            }

            _messageBus.Publish(message);
            DispatchToPlugins(message);

            // Broadcast to external plugin processes via IPC (named pipes)
            try
            {
                _processManager?.BroadcastEvent(message);
            }
            catch (Exception ex)
            {
                _logger.Error("Error broadcasting to external processes: " + ex.Message);
            }
        }

        public IReadOnlyList<PluginInfo> GetLoadedPlugins()
        {
            _pluginsLock.EnterReadLock();
            try
            {
                var result = new List<PluginInfo>();

                // In-process plugins
                foreach (var p in _plugins)
                {
                    result.Add(new PluginInfo
                    {
                        Name = p.Plugin.Name,
                        Version = p.Plugin.Version != null ? p.Plugin.Version.ToString() : "1.0.0",
                        State = p.State.ToString(),
                        LoadedAt = p.LoadedAt
                    });
                }

                // External process plugins (IPC via named pipes)
                if (_processManager != null)
                {
                    foreach (var status in _processManager.GetStatus())
                    {
                        result.Add(new PluginInfo
                        {
                            Name = status.PluginName + " (Process PID:" + status.ProcessId + ")",
                            Version = "1.0.0",
                            State = status.State,
                            LoadedAt = status.StartedAt
                        });
                    }
                }

                return result.AsReadOnly();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }
        }

        public IPlugin GetPlugin(string name)
        {
            _pluginsLock.EnterReadLock();
            try
            {
                var context = _plugins.FirstOrDefault(p =>
                    p.Plugin.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                return context?.Plugin;
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }
        }

        void IPluginHost.Publish(EventMessage evt)
        {
            Publish(evt);
        }

        void IPluginHost.Log(string message)
        {
            _logger.Info("[Plugin] " + message);
        }

        public IDisposable Subscribe(string topicPattern, Action<EventMessage> handler)
        {
            return _messageBus.Subscribe(topicPattern, handler);
        }

        public IReadOnlyList<string> GetAutoSubscriptions()
        {
            lock (_autoSubscriptionPatterns)
            {
                return _autoSubscriptionPatterns.ToList().AsReadOnly();
            }
        }

        public bool UnloadPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName)) return false;

            // Try to stop external process plugin first
            if (_processManager != null && _processManager.StopPlugin(pluginName))
            {
                _logger.Info("External plugin process unloaded: " + pluginName);
                return true;
            }

            _pluginsLock.EnterWriteLock();
            try
            {
                var context = _plugins.FirstOrDefault(p =>
                    p.Plugin.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
                if (context == null) return false;

                try
                {
                    if (context.State == PluginState.Running)
                    {
                        try
                        {
                            context.Plugin.Stop();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Error stopping plugin during unload: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    _plugins.Remove(context);
                }

                _logger.Info("Plugin unloaded: " + pluginName);
                return true;
            }
            finally
            {
                _pluginsLock.ExitWriteLock();
            }
        }

        // Actually crash a plugin - works with ANY plugin structure
        public bool CrashPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName)) return false;

            _pluginsLock.EnterWriteLock();
            try
            {
                var context = _plugins.FirstOrDefault(p =>
                    p.Plugin.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
                if (context == null) return false;

                if (context.State == PluginState.Faulted)
                {
                    _logger.Warn("Plugin " + pluginName + " is already in Faulted state.");
                    return true;
                }

                _logger.Error("╔══════════════════════════════════════════════════════════╗");
                _logger.Error("║  CRASH INITIATED: " + pluginName.PadRight(39) + "║");
                _logger.Error("╚══════════════════════════════════════════════════════════╝");

                // Step 1: Call Stop() to trigger normal shutdown
                try
                {
                    _logger.Info("Phase 1: Calling plugin.Stop().. .");
                    context.Plugin.Stop();
                    _logger.Info("Phase 1: Stop() completed.");
                }
                catch (Exception stopEx)
                {
                    _logger.Error("Phase 1: Exception during Stop(): " + stopEx.Message);
                }

                // Step 2: Use reflection to corrupt internal state
                try
                {
                    _logger.Info("Phase 2: Corrupting internal state via reflection...");
                    CorruptPluginState(context.Plugin);
                    _logger.Info("Phase 2: State corruption completed.");
                }
                catch (Exception corruptEx)
                {
                    _logger.Error("Phase 2: Exception during corruption: " + corruptEx.Message);
                }

                // Step 3: Mark as faulted
                context.State = PluginState.Faulted;
                context.LastError = "CRASHED at " + DateTime.Now.ToString("HH:mm:ss") + " - Forcefully terminated";

                _logger.Error("╔══════════════════════════════════════════════════════════╗");
                _logger.Error("║  CRASH COMPLETE: " + pluginName.PadRight(40) + "║");
                _logger.Error("║  Status: FAULTED - No longer receiving events            ║");
                _logger.Error("╚══════════════════════════════════════════════════════════╝");

                return true;
            }
            finally
            {
                _pluginsLock.ExitWriteLock();
            }
        }

        // Generic plugin state corruption - works with ANY plugin
        private void CorruptPluginState(IPlugin plugin)
        {
            Type pluginType = plugin.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            FieldInfo[] allFields = pluginType.GetFields(flags);
            int corruptedCount = 0;

            foreach (FieldInfo field in allFields)
            {
                try
                {
                    Type fieldType = field.FieldType;
                    object currentValue = field.GetValue(plugin);

                    if (currentValue == null) continue;

                    // Cancel CancellationTokenSource
                    if (fieldType == typeof(CancellationTokenSource))
                    {
                        var cts = currentValue as CancellationTokenSource;
                        if (cts != null && !cts.IsCancellationRequested)
                        {
                            _logger.Info("  → Cancelling: " + field.Name);
                            try
                            {
                                cts.Cancel();
                                corruptedCount++;
                            }
                            catch
                            {
                            }
                        }

                        continue;
                    }

                    // Wait for Tasks
                    if (typeof(Task).IsAssignableFrom(fieldType))
                    {
                        var task = currentValue as Task;
                        if (task != null && !task.IsCompleted)
                        {
                            _logger.Info("  → Awaiting task: " + field.Name);
                            try
                            {
                                task.Wait(2000);
                                corruptedCount++;
                            }
                            catch
                            {
                            }
                        }

                        continue;
                    }

                    // Dispose IDisposables
                    if (typeof(IDisposable).IsAssignableFrom(fieldType) && fieldType != typeof(CancellationTokenSource))
                    {
                        _logger.Info("  → Disposing: " + field.Name);
                        try
                        {
                            ((IDisposable)currentValue).Dispose();
                            corruptedCount++;
                        }
                        catch
                        {
                        }

                        continue;
                    }

                    // Set running/active booleans to false
                    if (fieldType == typeof(bool))
                    {
                        string nameLower = field.Name.ToLowerInvariant();
                        if (nameLower.Contains("running") || nameLower.Contains("active") ||
                            nameLower.Contains("started") || nameLower.Contains("enabled"))
                        {
                            _logger.Info("  → Disabling: " + field.Name);
                            field.SetValue(plugin, false);
                            corruptedCount++;
                        }

                        continue;
                    }

                    // Null out host references
                    if (fieldType == typeof(IPluginHost) || fieldType.Name.Contains("Host"))
                    {
                        _logger.Info("  → Nulling host: " + field.Name);
                        field.SetValue(plugin, null);
                        corruptedCount++;
                        continue;
                    }

                    // Null out other important-looking references
                    if (!fieldType.IsValueType && fieldType != typeof(string))
                    {
                        string nameLower = field.Name.ToLowerInvariant();
                        if (nameLower.Contains("random") || nameLower.Contains("timer") ||
                            nameLower.Contains("client") || nameLower.Contains("connection") ||
                            nameLower.Contains("stream") || nameLower.Contains("writer") ||
                            nameLower.Contains("reader") || nameLower.Contains("handler") ||
                            nameLower.Contains("service") || nameLower.Contains("logger"))
                        {
                            _logger.Info("  → Corrupting: " + field.Name);
                            field.SetValue(plugin, null);
                            corruptedCount++;
                        }
                    }
                }
                catch
                {
                    // Ignore reflection errors
                }
            }

            _logger.Info("  Corrupted " + corruptedCount + " field(s).");
        }

        // Restart a faulted plugin
        public bool RestartPlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName)) return false;

            _pluginsLock.EnterWriteLock();
            try
            {
                var context = _plugins.FirstOrDefault(p =>
                    p.Plugin.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
                if (context == null) return false;

                if (context.State == PluginState.Running)
                {
                    _logger.Warn("Plugin " + pluginName + " is already running.");
                    return true;
                }

                _logger.Info("Restarting plugin: " + pluginName);

                try
                {
                    context.State = PluginState.Starting;

                    var timeoutMs = (int)_configuration.PluginStartTimeout.TotalMilliseconds;
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var startTask = Task.Run(() => context.Plugin.Start(this), cts.Token);

                        if (!startTask.Wait(timeoutMs))
                        {
                            throw new TimeoutException("Plugin " + context.Plugin.Name + " restart timed out.");
                        }

                        if (startTask.Exception != null)
                        {
                            throw startTask.Exception.InnerException ?? startTask.Exception;
                        }
                    }

                    context.State = PluginState.Running;
                    context.LastError = null;
                    _logger.Info("Plugin restarted: " + pluginName);
                    return true;
                }
                catch (Exception ex)
                {
                    context.State = PluginState.Faulted;
                    context.LastError = ex.Message;
                    _logger.Error("Failed to restart plugin " + pluginName + ": " + ex.Message);
                    return false;
                }
            }
            finally
            {
                _pluginsLock.ExitWriteLock();
            }
        }

        private void LoadPlugins()
        {
            var searchOption = _configuration.SearchSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var loadedPlugins = _pluginLoader.LoadPlugins(
                _configuration.PluginsDirectory,
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

                    var timeoutMs = (int)_configuration.PluginStartTimeout.TotalMilliseconds;
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var startTask = Task.Run(() => context.Plugin.Start(this), cts.Token);

                        if (!startTask.Wait(timeoutMs))
                        {
                            throw new TimeoutException("Plugin " + context.Plugin.Name + " start timed out.");
                        }

                        if (startTask.Exception != null)
                        {
                            throw startTask.Exception.InnerException ?? startTask.Exception;
                        }
                    }

                    context.State = PluginState.Running;
                }
                catch (Exception ex)
                {
                    context.State = PluginState.Faulted;
                    context.LastError = ex.Message;
                    _logger.Error("Failed to start plugin " + context.Plugin.Name + ": " + ex.Message);

                    if (!_configuration.ContinueOnPluginError)
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
                pluginsToStop = _plugins.Where(p => p.State == PluginState.Running).ToList();
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }

            pluginsToStop.Reverse();

            foreach (var context in pluginsToStop)
            {
                try
                {
                    context.State = PluginState.Stopping;

                    var timeoutMs = (int)_configuration.PluginStopTimeout.TotalMilliseconds;
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var stopTask = Task.Run(() => context.Plugin.Stop(), cts.Token);
                        stopTask.Wait(timeoutMs);
                    }

                    context.State = PluginState.Stopped;
                }
                catch (Exception ex)
                {
                    context.State = PluginState.Faulted;
                    context.LastError = ex.Message;
                    _logger.Error("Error stopping plugin " + context.Plugin.Name + ": " + ex.Message);
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
                _pluginsLock.ExitWriteLock();
            }
        }

        private void DispatchToPlugins(EventMessage message)
        {
            _pluginsLock.EnterReadLock();
            List<PluginContext> activePlugins;
            try
            {
                // Only dispatch to Running plugins - Faulted plugins don't receive events
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
                    // When a plugin throws an exception during HandleEvent, mark it as Faulted
                    _logger.Error("Plugin " + context.Plugin.Name + " crashed during HandleEvent: " + ex.Message);

                    _pluginsLock.EnterWriteLock();
                    try
                    {
                        context.State = PluginState.Faulted;
                        context.LastError = ex.Message;
                    }
                    finally
                    {
                        _pluginsLock.ExitWriteLock();
                    }

                    _logger.Error("Plugin " + context.Plugin.Name +
                                  " is now in Faulted state and will not receive further events.");
                }
            }
        }

        private void TryCreateAutoMetricsSubscription()
        {
            _pluginsLock.EnterReadLock();
            try
            {
                bool hasMetricsLogger = _plugins.Any(p =>
                    p.Plugin.Name.Equals("MetricsLogger", StringComparison.OrdinalIgnoreCase));
                if (!hasMetricsLogger)
                {
                    return;
                }
            }
            finally
            {
                _pluginsLock.ExitReadLock();
            }

            try
            {
                if (_autoMetricsSubscription != null) return;

                _autoMetricsSubscription = _messageBus.Subscribe("metrics.*",
                    (evt) =>
                    {
                        _logger.Info("[subscription metrics.*] " + (evt?.Topic ?? "") + ": " + (evt?.Payload ?? ""));
                    });

                lock (_autoSubscriptionPatterns)
                {
                    if (!_autoSubscriptionPatterns.Contains("metrics.*"))
                    {
                        _autoSubscriptionPatterns.Add("metrics.*");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create auto metrics subscription: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets status of external plugin processes managed via IPC. 
        /// </summary>
        public IReadOnlyList<PluginProcessStatus> GetExternalPluginStatus()
        {
            return _processManager?.GetStatus() ?? new List<PluginProcessStatus>();
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            if (State == KernelState.Running)
            {
                Stop();
            }

            try
            {
                _processManager?.Dispose();
            }
            catch
            {
            }

            _pluginsLock.Dispose();
            _disposed = true;
        }
    }
}