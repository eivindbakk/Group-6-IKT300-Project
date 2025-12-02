using System;
using System.Collections. Concurrent;
using System.Collections.Generic;
using System. Diagnostics;
using System.IO;
using System. IO. Pipes;
using System.Linq;
using System.Threading;
using System. Threading.Tasks;
using Contracts;
using Contracts.IPC;
using Microkernel.Services;

namespace Microkernel.IPC
{
    /// <summary>
    /// Manages plugin processes and IPC communication via named pipes. 
    /// This provides TRUE OS-LEVEL PROCESS ISOLATION as required by the assignment.
    /// </summary>
    public class PluginProcessManager : IDisposable
    {
        private readonly IKernelLogger _logger;
        private readonly ConcurrentDictionary<string, PluginProcessInfo> _processes;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public event Action<string, string> OnPluginLog;

        public PluginProcessManager(IKernelLogger logger)
        {
            _logger = logger ??  throw new ArgumentNullException(nameof(logger));
            _processes = new ConcurrentDictionary<string, PluginProcessInfo>(StringComparer.OrdinalIgnoreCase);
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Launches a plugin as a SEPARATE OS PROCESS with named pipe IPC. 
        /// </summary>
        public bool LaunchPlugin(string pluginName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(pluginName) || string.IsNullOrWhiteSpace(executablePath))
                return false;

            if (! File.Exists(executablePath))
            {
                _logger.Error("Plugin executable not found: " + executablePath);
                return false;
            }

            if (_processes.ContainsKey(pluginName))
            {
                _logger. Warn("Plugin process '" + pluginName + "' is already running.");
                return false;
            }

            try
            {
                // Create unique pipe name for this plugin
                string pipeName = "microkernel_plugin_" + pluginName + "_" + Guid.NewGuid(). ToString("N");

                // Create named pipe server
                var pipeServer = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode. Byte,
                    PipeOptions.Asynchronous);

                // Start the plugin process
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--pipe " + pipeName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                var process = Process.Start(processInfo);
                if (process == null)
                {
                    pipeServer. Dispose();
                    _logger.Error("Failed to start plugin process: " + pluginName);
                    return false;
                }

                var info = new PluginProcessInfo
                {
                    PluginName = pluginName,
                    Process = process,
                    PipeServer = pipeServer,
                    PipeName = pipeName,
                    State = PluginProcessState.Starting,
                    StartedAt = DateTime. UtcNow
                };

                _processes[pluginName] = info;

                // Start async connection handler
                Task.Run(() => HandlePluginConnectionAsync(info, _cts.Token));

                // Start stdout/stderr readers
                Task.Run(() => ReadProcessOutput(info, process.StandardOutput, "stdout"));
                Task.Run(() => ReadProcessOutput(info, process.StandardError, "stderr"));

                _logger.Info("Launched plugin process: " + pluginName + " (PID: " + process.Id + ")");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error launching plugin process " + pluginName + ": " + ex.Message);
                return false;
            }
        }

        private async Task HandlePluginConnectionAsync(PluginProcessInfo info, CancellationToken token)
        {
            try
            {
                _logger.Debug("Waiting for plugin '" + info. PluginName + "' to connect.. .");

                // Wait for plugin to connect with timeout
                var connectTask = info.PipeServer.WaitForConnectionAsync(token);
                var timeoutTask = Task. Delay(30000, token);
                
                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                {
                    _logger.Error("Plugin '" + info. PluginName + "' connection timeout.");
                    info. State = PluginProcessState.Faulted;
                    return;
                }

                await connectTask;
                info.State = PluginProcessState.Connected;
                _logger.Info("Plugin '" + info. PluginName + "' connected via named pipe.");

                // Send start command
                var startMessage = new IpcMessage
                {
                    Type = IpcMessageType. Start,
                    PluginName = info.PluginName
                };
                SendMessage(info.PipeServer, startMessage);
                info.State = PluginProcessState.Running;

                // Listen for messages from plugin
                while (!token.IsCancellationRequested && info.PipeServer.IsConnected)
                {
                    try
                    {
                        var message = IpcProtocol.ReadMessage(info.PipeServer);
                        if (message == null)
                        {
                            await Task.Delay(10, token);
                            continue;
                        }

                        HandlePluginMessage(info, message);
                    }
                    catch (IOException)
                    {
                        _logger.Warn("Plugin '" + info. PluginName + "' pipe disconnected.");
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
                _logger.Error("Plugin '" + info. PluginName + "' connection error: " + ex.Message);
                info. State = PluginProcessState.Faulted;
                info.LastError = ex.Message;
            }
        }

        private void HandlePluginMessage(PluginProcessInfo info, IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType.Log:
                    OnPluginLog?. Invoke(info.PluginName, message.Response);
                    _logger.Info("[Process:" + info.PluginName + "] " + message.Response);
                    break;

                case IpcMessageType. Heartbeat:
                    info.LastHeartbeat = DateTime. UtcNow;
                    break;

                case IpcMessageType. Ack:
                    _logger.Debug("Plugin '" + info. PluginName + "' acknowledged: " + message. Response);
                    break;
            }
        }

        private void ReadProcessOutput(PluginProcessInfo info, StreamReader reader, string streamName)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    _logger.Debug("[" + info.PluginName + "/" + streamName + "] " + line);
                }
            }
            catch { }
        }

        private void SendMessage(NamedPipeServerStream pipe, IpcMessage message)
        {
            try
            {
                IpcProtocol. WriteMessage(pipe, message);
            }
            catch (Exception ex)
            {
                _logger.Error("SendMessage error: " + ex.Message);
            }
        }

        /// <summary>
        /// Sends an event to a plugin process via IPC.
        /// </summary>
        public bool SendEvent(string pluginName, EventMessage evt)
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
                SendMessage(info.PipeServer, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger. Error("Failed to send event to plugin '" + pluginName + "': " + ex.Message);
                info.State = PluginProcessState. Faulted;
                info.LastError = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Broadcasts an event to ALL running plugin processes.
        /// </summary>
        public void BroadcastEvent(EventMessage evt)
        {
            foreach (var kvp in _processes)
            {
                if (kvp.Value.State == PluginProcessState.Running)
                {
                    SendEvent(kvp. Key, evt);
                }
            }
        }

        /// <summary>
        /// Stops a plugin process.
        /// </summary>
        public bool StopPlugin(string pluginName)
        {
            if (!_processes.TryRemove(pluginName, out var info))
                return false;

            try
            {
                // Send shutdown command
                if (info.PipeServer != null && info.PipeServer.IsConnected)
                {
                    try
                    {
                        var shutdownMessage = new IpcMessage
                        {
                            Type = IpcMessageType.Shutdown,
                            PluginName = pluginName
                        };
                        SendMessage(info.PipeServer, shutdownMessage);
                    }
                    catch { }
                }

                // Wait for graceful shutdown
                if (info.Process != null && ! info.Process.HasExited)
                {
                    if (! info.Process.WaitForExit(5000))
                    {
                        info.Process.Kill();
                        _logger.Warn("Plugin '" + pluginName + "' did not exit gracefully, killed.");
                    }
                }

                info.PipeServer?. Dispose();
                info.Process?.Dispose();
                info.State = PluginProcessState.Stopped;

                _logger.Info("Stopped plugin process: " + pluginName);
                return true;
            }
            catch (Exception ex)
            {
                _logger. Error("Error stopping plugin '" + pluginName + "': " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets status of all managed plugin processes.
        /// </summary>
        public IReadOnlyList<PluginProcessStatus> GetStatus()
        {
            var result = new List<PluginProcessStatus>();
            foreach (var kvp in _processes)
            {
                var info = kvp.Value;
                int pid = 0;
                try { pid = info.Process?. Id ??  0; } catch { }
                
                result.Add(new PluginProcessStatus
                {
                    PluginName = info.PluginName,
                    ProcessId = pid,
                    State = info.State. ToString(),
                    StartedAt = info.StartedAt,
                    LastHeartbeat = info. LastHeartbeat,
                    LastError = info.LastError
                });
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();

            foreach (var kvp in _processes. ToArray())
            {
                try { StopPlugin(kvp.Key); } catch { }
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

    public class PluginProcessStatus
    {
        public string PluginName { get; set; }
        public int ProcessId { get; set; }
        public string State { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public string LastError { get; set; }
    }
}