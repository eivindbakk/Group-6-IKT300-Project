using System;
using System. Collections. Concurrent;
using System.Collections.Generic;
using System. Diagnostics;
using System.IO;
using System. IO.Pipes;
using System.Text;
using System. Text.Json;
using System. Threading;
using System.Threading.Tasks;
using Contracts;
using Contracts.IPC;
using Microkernel.Services;

namespace Microkernel.IPC
{
    /// <summary>
    /// Manages plugin processes and IPC communication via named pipes. 
    /// This provides true OS-level process isolation as required by the assignment.
    /// </summary>
    public class PluginProcessManager : IDisposable
    {
        private readonly IKernelLogger _logger;
        private readonly ConcurrentDictionary<string, PluginProcessInfo> _processes;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public event Action<string, EventMessage> OnPluginEvent;
        public event Action<string, string> OnPluginLog;

        public PluginProcessManager(IKernelLogger logger)
        {
            _logger = logger ??  throw new ArgumentNullException(nameof(logger));
            _processes = new ConcurrentDictionary<string, PluginProcessInfo>();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Launches a plugin as a separate OS process with named pipe IPC.
        /// </summary>
        public bool LaunchPluginProcess(string pluginName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(pluginName) || string. IsNullOrWhiteSpace(executablePath))
                return false;

            if (_processes.ContainsKey(pluginName))
            {
                _logger. Warn($"Plugin process {pluginName} is already running.");
                return false;
            }

            try
            {
                string pipeName = $"microkernel_plugin_{pluginName}_{Guid.NewGuid():N}";

                // Create the named pipe server
                var pipeServer = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode. Byte,
                    PipeOptions. Asynchronous);

                // Start the plugin process
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = $"--pipe {pipeName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(processInfo);
                if (process == null)
                {
                    pipeServer. Dispose();
                    _logger.Error($"Failed to start plugin process: {pluginName}");
                    return false;
                }

                var pluginInfo = new PluginProcessInfo
                {
                    PluginName = pluginName,
                    Process = process,
                    PipeServer = pipeServer,
                    PipeName = pipeName,
                    State = PluginProcessState. Starting,
                    StartedAt = DateTime. UtcNow
                };

                _processes[pluginName] = pluginInfo;

                // Start listening for connection and messages
                Task.Run(() => HandlePluginConnection(pluginInfo, _cts.Token));

                _logger. Info($"Launched plugin process: {pluginName} (PID: {process.Id})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error launching plugin process {pluginName}: {ex.Message}");
                return false;
            }
        }

        private async Task HandlePluginConnection(PluginProcessInfo info, CancellationToken token)
        {
            try
            {
                // Wait for plugin to connect
                await info.PipeServer.WaitForConnectionAsync(token);
                info.State = PluginProcessState.Connected;
                _logger.Info($"Plugin {info. PluginName} connected via named pipe.");

                // Send start command
                var startMessage = new IpcMessage
                {
                    Type = IpcMessageType. Start,
                    PluginName = info.PluginName
                };
                IpcProtocol.WriteMessage(info.PipeServer, startMessage);
                info.State = PluginProcessState.Running;

                // Listen for messages from plugin
                while (! token.IsCancellationRequested && info.PipeServer.IsConnected)
                {
                    try
                    {
                        var message = IpcProtocol. ReadMessage(info. PipeServer);
                        if (message == null) break;

                        HandlePluginMessage(info. PluginName, message);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.Error($"Plugin {info.PluginName} connection error: {ex. Message}");
                info.State = PluginProcessState. Faulted;
                info.LastError = ex.Message;
            }
        }

        private void HandlePluginMessage(string pluginName, IpcMessage message)
        {
            switch (message. Type)
            {
                case IpcMessageType. Publish:
                    if (message.Event != null)
                    {
                        OnPluginEvent?. Invoke(pluginName, message.Event);
                    }
                    break;

                case IpcMessageType.Log:
                    OnPluginLog?. Invoke(pluginName, message.Response);
                    break;

                case IpcMessageType. Heartbeat:
                    if (_processes.TryGetValue(pluginName, out var info))
                    {
                        info.LastHeartbeat = DateTime.UtcNow;
                    }
                    break;
            }
        }

        /// <summary>
        /// Sends an event to a plugin process via IPC.
        /// </summary>
        public bool SendEventToPlugin(string pluginName, EventMessage evt)
        {
            if (! _processes.TryGetValue(pluginName, out var info))
                return false;

            if (info.State != PluginProcessState.Running)
                return false;

            try
            {
                var message = new IpcMessage
                {
                    Type = IpcMessageType.Event,
                    PluginName = pluginName,
                    Event = evt
                };
                IpcProtocol. WriteMessage(info. PipeServer, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to send event to plugin {pluginName}: {ex.Message}");
                info.State = PluginProcessState. Faulted;
                info.LastError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Broadcasts an event to all running plugin processes.
        /// </summary>
        public void BroadcastEvent(EventMessage evt)
        {
            foreach (var kvp in _processes)
            {
                if (kvp. Value.State == PluginProcessState. Running)
                {
                    SendEventToPlugin(kvp.Key, evt);
                }
            }
        }

        /// <summary>
        /// Stops a plugin process. 
        /// </summary>
        public bool StopPluginProcess(string pluginName)
        {
            if (!_processes.TryRemove(pluginName, out var info))
                return false;

            try
            {
                // Send shutdown command
                if (info.PipeServer.IsConnected)
                {
                    var shutdownMessage = new IpcMessage
                    {
                        Type = IpcMessageType.Shutdown,
                        PluginName = pluginName
                    };
                    IpcProtocol.WriteMessage(info.PipeServer, shutdownMessage);
                }

                // Wait for graceful shutdown
                if (! info.Process.WaitForExit(5000))
                {
                    info. Process.Kill();
                    _logger. Warn($"Plugin {pluginName} did not exit gracefully, killed.");
                }

                info.PipeServer.Dispose();
                info. Process.Dispose();
                info.State = PluginProcessState. Stopped;

                _logger.Info($"Stopped plugin process: {pluginName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping plugin {pluginName}: {ex. Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets information about all managed plugin processes.
        /// </summary>
        public IReadOnlyList<PluginProcessInfo> GetProcessInfo()
        {
            return new List<PluginProcessInfo>(_processes.Values);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();

            foreach (var kvp in _processes)
            {
                try
                {
                    StopPluginProcess(kvp.Key);
                }
                catch { }
            }

            _cts.Dispose();
            _disposed = true;
        }
    }

    public class PluginProcessInfo
    {
        public string PluginName { get; set; }
        public Process Process { get; set; }
        public NamedPipeServerStream PipeServer { get; set; }
        public string PipeName { get; set; }
        public PluginProcessState State { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public string LastError { get; set; }
    }

    public enum PluginProcessState
    {
        Starting,
        Connected,
        Running,
        Stopping,
        Stopped,
        Faulted
    }
}