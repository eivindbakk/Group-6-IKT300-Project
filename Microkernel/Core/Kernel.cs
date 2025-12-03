using System;
using System.Collections.Generic;
using System.IO;
using Contracts;
using Microkernel.IPC;
using Microkernel.Messaging;
using Microkernel.Services;

namespace Microkernel.Core
{
    /// <summary>
    /// The Microkernel core - responsible ONLY for:
    /// 1. Process Management (launching, monitoring, terminating plugin processes)
    /// 2. IPC (Inter-Process Communication via named pipes)
    /// 3. Message routing between plugins
    /// 
    /// All business logic resides in plugin processes. 
    /// </summary>
    public sealed class Kernel : IKernel, IDisposable
    {
        private readonly KernelConfiguration _configuration;
        private readonly IMessageBus _messageBus;
        private readonly IKernelLogger _logger;
        private readonly PluginProcessManager _processManager;
        private readonly object _stateLock = new object();

        private KernelState _state = KernelState.Created;
        private bool _disposed;


        public Kernel(KernelConfiguration configuration, IMessageBus messageBus, IKernelLogger logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processManager = new PluginProcessManager(logger);

            _processManager.OnPluginPublish += HandlePluginPublish;
        }

        public static Kernel CreateDefault()
        {
            return CreateDefault(KernelConfiguration.CreateDefault());
        }

        private void HandlePluginPublish(EventMessage evt)
        {
            if (evt == null) return;

            _logger.Debug("Plugin published: " + evt.Topic);

            _processManager.BroadcastEventExcept(evt, evt.Source ?? "");

            _messageBus.Publish(evt);
        }

        public static Kernel CreateDefault(KernelConfiguration configuration)
        {
            configuration = configuration ?? KernelConfiguration.CreateDefault();
            var logger = new ConsoleKernelLogger();
            var messageBus = new MessageBus(logger);
            return new Kernel(configuration, messageBus, logger);
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

            _logger.Info("Kernel starting...");

            try
            {
                LaunchPluginProcesses();
                State = KernelState.Running;

                int pluginCount = _processManager.GetStatus().Count;
                _logger.Info("Kernel started. " + pluginCount + " plugin process(es) active.");
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
                if (_state != KernelState.Running)
                    return;
                _state = KernelState.Stopping;
            }

            _logger.Info("Kernel stopping...");

            try
            {
                _processManager.StopAllPlugins();
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
                throw new ArgumentNullException(nameof(message));

            if (State != KernelState.Running)
                return;

            if (message.Timestamp == default)
                message.Timestamp = DateTime.UtcNow;

            // Publish to internal message bus
            _messageBus.Publish(message);

            // Broadcast to all external plugin processes via IPC
            _processManager.BroadcastEvent(message);
        }

        public IReadOnlyList<PluginInfo> GetLoadedPlugins()
        {
            var result = new List<PluginInfo>();

            foreach (var status in _processManager.GetStatus())
            {
                result.Add(new PluginInfo
                {
                    Name = status.PluginName,
                    Version = "1.0.0",
                    State = status.State,
                    LoadedAt = status.StartedAt,
                    ProcessId = status.ProcessId
                });
            }

            return result.AsReadOnly();
        }

        public (int total, int running, int faulted) GetPluginCounts()
        {
            return _processManager.GetCounts();
        }

        public bool CrashPlugin(string pluginName)
        {
            return _processManager.KillPlugin(pluginName);
        }

        public bool RestartPlugin(string pluginName)
        {
            return _processManager.RestartPlugin(pluginName);
        }

        /// <summary>
        /// Dynamically loads a plugin process at runtime. 
        /// </summary>
        public bool LoadPlugin(string pluginName, string executablePath)
        {
            if (State != KernelState.Running)
            {
                _logger.Warn("Cannot load plugin - kernel not running.");
                return false;
            }

            return _processManager.LaunchPlugin(pluginName, executablePath);
        }

        public bool UnloadPlugin(string pluginName)
        {
            return _processManager.StopPlugin(pluginName);
        }

        public IDisposable Subscribe(string topicPattern, Action<EventMessage> handler)
        {
            return _messageBus.Subscribe(topicPattern, handler);
        }

        /// <summary>
        /// Discovers and launches all plugin executables in the same folder as the kernel. 
        /// 
        /// To add a new plugin:
        /// 1.  Build the plugin project
        /// 2. Copy the . exe, .dll, and .runtimeconfig.json to the kernel's output folder
        /// 3. Restart the kernel
        /// 
        /// No kernel code changes required - plugins are discovered automatically.
        /// </summary>
        private void LaunchPluginProcesses()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            _logger.Info("Discovering plugins in: " + baseDir);

            // Executables that are NOT plugins
            var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Microkernel.exe",
                "createdump.exe"
            };

            int pluginsFound = 0;
            int pluginsLaunched = 0;

            try
            {
                var exeFiles = Directory.GetFiles(baseDir, "*.exe");

                foreach (var exePath in exeFiles)
                {
                    string fileName = Path.GetFileName(exePath);

                    // Skip non-plugin executables
                    if (excludedFiles.Contains(fileName))
                        continue;

                    // A valid . NET plugin has a matching .dll file
                    string dllPath = Path.ChangeExtension(exePath, ".dll");
                    if (!File.Exists(dllPath))
                        continue;

                    pluginsFound++;

                    // Derive plugin name: "MetricsLogger. exe" -> "MetricsLoggerProcess"
                    string pluginName = Path.GetFileNameWithoutExtension(exePath) + "Process";

                    _logger.Debug("Discovered plugin: " + pluginName + " (" + fileName + ")");

                    if (_processManager.LaunchPlugin(pluginName, exePath))
                    {
                        pluginsLaunched++;
                        _logger.Info(pluginName + " launched as separate OS process.");
                    }
                    else
                    {
                        _logger.Warn("Failed to launch plugin: " + pluginName);
                    }
                }

                if (pluginsFound == 0)
                {
                    _logger.Warn("No plugins found.  Place plugin . exe files in: " + baseDir);
                }
                else
                {
                    _logger.Info("Plugins: " + pluginsLaunched + "/" + pluginsFound + " launched successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error discovering plugins: " + ex.Message);

                if (!_configuration.ContinueOnPluginError)
                    throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (State == KernelState.Running)
                Stop();

            _processManager?.Dispose();
            _disposed = true;
        }
    }
}