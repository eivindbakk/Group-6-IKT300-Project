using System;
using System.Collections. Concurrent;
using System.Collections.Generic;
using System. Diagnostics;
using System.IO;
using System. IO.Pipes;
using System.Linq;
using System. Text. Json;
using System. Threading;
using Contracts;
using Contracts.IPC;
using Microkernel.Services;

namespace Microkernel.IPC
{
    public class PluginProcessManager : IDisposable
    {
        private readonly IKernelLogger _logger;
        private readonly ConcurrentDictionary<string, PluginProcessInfo> _processes;
        private readonly ConcurrentDictionary<string, string> _pluginStates;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public event Action<EventMessage> OnPluginPublish;

        public PluginProcessManager(IKernelLogger logger)
        {
            _logger = logger ??  throw new ArgumentNullException(nameof(logger));
            _processes = new ConcurrentDictionary<string, PluginProcessInfo>(StringComparer.OrdinalIgnoreCase);
            _pluginStates = new ConcurrentDictionary<string, string>(StringComparer. OrdinalIgnoreCase);
            _cts = new CancellationTokenSource();
        }

        private void SetPluginState(string pluginName, string state)
        {
            _pluginStates[pluginName] = state;
        }

        private string GetPluginState(string pluginName)
        {
            return _pluginStates.TryGetValue(pluginName, out var state) ? state : "Starting";
        }

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
                _logger. Warn("Plugin '" + pluginName + "' is already running.");
                return false;
            }

            try
            {
                string pipeName = "microkernel_" + pluginName + "_" + Guid.NewGuid(). ToString("N");

                var pipeServer = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--pipe " + pipeName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    pipeServer. Dispose();
                    _logger.Error("Failed to start plugin process: " + pluginName);
                    return false;
                }

                var info = new PluginProcessInfo
                {
                    PluginName = pluginName,
                    ExecutablePath = executablePath,
                    Process = process,
                    PipeServer = pipeServer,
                    PipeName = pipeName,
                    StartedAt = DateTime. UtcNow
                };

                _processes[pluginName] = info;
                SetPluginState(pluginName, "Starting");

                new Thread(() => ConnectionHandler(info)) { IsBackground = true }. Start();
                new Thread(() => ProcessMonitor(info)) { IsBackground = true }. Start();
                new Thread(() => ConsumeStream(process.StandardOutput, pluginName)) { IsBackground = true }.Start();
                new Thread(() => ConsumeStream(process. StandardError, pluginName)) { IsBackground = true }.Start();

                _logger.Info("Launched plugin process: " + pluginName + " (PID: " + process.Id + ")");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error launching plugin '" + pluginName + "': " + ex. Message);
                return false;
            }
        }

        private void ConnectionHandler(PluginProcessInfo info)
        {
            string pluginName = info.PluginName;

            try
            {
                var ar = info.PipeServer. BeginWaitForConnection(null, null);
                if (! ar.AsyncWaitHandle.WaitOne(30000))
                {
                    _logger.Error("Plugin '" + pluginName + "' connection timeout.");
                    SetPluginState(pluginName, "Faulted");
                    info.LastError = "Connection timeout";
                    return;
                }

                info.PipeServer. EndWaitForConnection(ar);
                _logger.Info("Plugin '" + pluginName + "' connected.");

                // IMPORTANT: Set state to Running BEFORE sending START command
                // This avoids the blocking issue where WriteMessage blocks
                SetPluginState(pluginName, "Running");

                // Send START command in a separate thread to avoid blocking the connection handler
                new Thread(() =>
                {
                    try
                    {
                        IpcProtocol.WriteMessage(info.PipeServer, new IpcMessage
                        {
                            Type = IpcMessageType.Start,
                            PluginName = pluginName
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Failed to send START to '" + pluginName + "': " + ex.Message);
                    }
                }) { IsBackground = true }.Start();

                // Listen for messages from the plugin
                ListenForMessages(info);
            }
            catch (ObjectDisposedException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.Error("Plugin '" + pluginName + "' connection error: " + ex.Message);
                SetPluginState(pluginName, "Faulted");
                info.LastError = ex.Message;
            }
        }

        private void ListenForMessages(PluginProcessInfo info)
        {
            string pluginName = info.PluginName;

            while (!_cts. IsCancellationRequested &&
                   _processes.ContainsKey(pluginName) &&
                   info.PipeServer != null &&
                   info. PipeServer.IsConnected)
            {
                try
                {
                    var message = IpcProtocol.ReadMessage(info.PipeServer);
                    if (message == null)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    ProcessPluginMessage(info, message);
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (JsonException ex)
                {
                    _logger. Error("Invalid message from '" + pluginName + "': " + ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error from '" + pluginName + "': " + ex.Message);
                }
            }

            // Mark as faulted if we were running and disconnected unexpectedly
            if (_processes.ContainsKey(pluginName) && GetPluginState(pluginName) == "Running")
            {
                SetPluginState(pluginName, "Faulted");
                info.LastError = "Disconnected";
            }
        }

        private void ProcessPluginMessage(PluginProcessInfo info, IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType. Heartbeat:
                    info.LastHeartbeat = DateTime.UtcNow;
                    break;

                case IpcMessageType.Publish:
                    if (message.Event != null)
                    {
                        OnPluginPublish?. Invoke(message.Event);
                    }
                    break;

                case IpcMessageType. Ack:
                    // Plugin acknowledged a command
                    break;
            }
        }

        private void ProcessMonitor(PluginProcessInfo info)
        {
            try
            {
                info.Process?. WaitForExit();

                if (_processes.ContainsKey(info. PluginName) && GetPluginState(info.PluginName) == "Running")
                {
                    int exitCode = info.Process?. ExitCode ?? -1;
                    _logger.Error("Plugin '" + info.PluginName + "' crashed (exit code: " + exitCode + ")");
                    SetPluginState(info. PluginName, "Faulted");
                    info.LastError = "Crashed with exit code " + exitCode;
                    _logger.Info("Kernel continues running.  Other plugins unaffected.");
                }
            }
            catch { }
        }

        public bool KillPlugin(string pluginName)
        {
            if (! _processes.TryGetValue(pluginName, out var info))
            {
                _logger.Warn("Plugin not found: " + pluginName);
                return false;
            }

            try
            {
                _logger. Warn("Forcefully killing plugin: " + pluginName);

                if (info.Process != null && ! info.Process.HasExited)
                {
                    info.Process.Kill();
                }

                SetPluginState(pluginName, "Faulted");
                info.LastError = "Forcefully killed";
                _logger.Info("Plugin '" + pluginName + "' killed.  Other plugins continue running.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error killing plugin '" + pluginName + "': " + ex. Message);
                return false;
            }
        }

        public bool RestartPlugin(string pluginName)
        {
            if (! _processes.TryGetValue(pluginName, out var info))
            {
                _logger. Warn("Plugin not found: " + pluginName);
                return false;
            }

            string executablePath = info. ExecutablePath;

            _logger.Info("Restarting plugin: " + pluginName);

            StopPlugin(pluginName);
            Thread.Sleep(500);

            return LaunchPlugin(pluginName, executablePath);
        }

        public bool StopPlugin(string pluginName)
        {
            if (! _processes.TryRemove(pluginName, out var info))
                return false;

            _pluginStates.TryRemove(pluginName, out _);

            try
            {
                // Kill the process first to avoid blocking on pipe write
                if (info.Process != null && !info.Process. HasExited)
                {
                    try { info.Process.Kill(); } catch { }
                    try { info.Process.WaitForExit(1000); } catch { }
                }

                // Then dispose resources
                try { info.PipeServer?.Dispose(); } catch { }
                try { info. Process?.Dispose(); } catch { }

                _logger.Info("Stopped plugin: " + pluginName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Error stopping plugin '" + pluginName + "': " + ex.Message);
                return false;
            }
        }

        public void StopAllPlugins()
        {
            foreach (var pluginName in _processes. Keys. ToArray())
            {
                StopPlugin(pluginName);
            }
        }

        public bool SendEvent(string pluginName, EventMessage evt)
        {
            if (! _processes.TryGetValue(pluginName, out var info))
            {
                _logger.Debug("SendEvent: Plugin not found: " + pluginName);
                return false;
            }

            string state = GetPluginState(pluginName);
            if (state != "Running")
            {
                _logger.Debug("SendEvent: Plugin '" + pluginName + "' not running (state: " + state + ")");
                return false;
            }

            try
            {
                if (info.PipeServer == null || ! info.PipeServer.IsConnected)
                {
                    _logger. Debug("SendEvent: Pipe not connected for '" + pluginName + "'");
                    return false;
                }

                _logger.Debug("SendEvent: Sending '" + evt.Topic + "' to '" + pluginName + "'");

                IpcProtocol. WriteMessage(info.PipeServer, new IpcMessage
                {
                    Type = IpcMessageType.Event,
                    PluginName = pluginName,
                    Event = evt
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send event to '" + pluginName + "': " + ex.Message);
                SetPluginState(pluginName, "Faulted");
                info.LastError = ex.Message;
                return false;
            }
        }

        public void BroadcastEvent(EventMessage evt)
        {
            foreach (var pluginName in _processes.Keys. ToArray())
            {
                if (GetPluginState(pluginName) == "Running")
                {
                    SendEvent(pluginName, evt);
                }
            }
        }

        public void BroadcastEventExcept(EventMessage evt, string excludePluginName)
        {
            _logger.Debug("BroadcastEventExcept: Broadcasting '" + evt.Topic + "' (exclude: '" + excludePluginName + "')");

            foreach (var pluginName in _processes.Keys. ToArray())
            {
                string state = GetPluginState(pluginName);
                bool isExcluded = ! string.IsNullOrEmpty(excludePluginName) &&
                                  pluginName. Contains(excludePluginName, StringComparison. OrdinalIgnoreCase);

                _logger.Debug("  -> Plugin '" + pluginName + "': state=" + state + ", excluded=" + isExcluded);

                if (state == "Running" && ! isExcluded)
                {
                    SendEvent(pluginName, evt);
                }
            }
        }

        public IReadOnlyList<PluginProcessStatus> GetStatus()
        {
            var result = new List<PluginProcessStatus>();

            foreach (var kvp in _processes)
            {
                var info = kvp.Value;
                string pluginName = kvp.Key;

                int pid = 0;
                bool processExited = true;

                try
                {
                    if (info.Process != null && !info. Process.HasExited)
                    {
                        pid = info.Process.Id;
                        processExited = false;
                    }
                }
                catch { }

                string state = GetPluginState(pluginName);

                // If process exited but state is Running, it's faulted
                if (processExited && state == "Running")
                {
                    state = "Faulted";
                    SetPluginState(pluginName, "Faulted");
                }

                result.Add(new PluginProcessStatus
                {
                    PluginName = pluginName,
                    ProcessId = pid,
                    State = state,
                    StartedAt = info. StartedAt,
                    LastHeartbeat = info.LastHeartbeat,
                    LastError = info.LastError
                });
            }

            return result;
        }

        public (int total, int running, int faulted) GetCounts()
        {
            int total = 0;
            int running = 0;
            int faulted = 0;

            foreach (var pluginName in _processes.Keys)
            {
                total++;
                string state = GetPluginState(pluginName);
                if (state == "Faulted")
                    faulted++;
                else if (state == "Running")
                    running++;
            }

            return (total, running, faulted);
        }

        private void ConsumeStream(StreamReader reader, string pluginName)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (! string.IsNullOrWhiteSpace(line))
                    {
                        _logger.Debug("[" + pluginName + "] " + line);
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _cts.Cancel();
            StopAllPlugins();
            _cts.Dispose();
            _disposed = true;
        }
    }

    public class PluginProcessInfo
    {
        public string PluginName { get; set; }
        public string ExecutablePath { get; set; }
        public Process Process { get; set; }
        public NamedPipeServerStream PipeServer { get; set; }
        public string PipeName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public string LastError { get; set; }
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