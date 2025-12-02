using System;
using System. IO. Pipes;
using System.Text. Json;
using System.Threading;
using Contracts;
using Contracts.Events;
using Contracts.IPC;

namespace Plugins.MetricsLoggerProcess
{
    /// <summary>
    /// Standalone process entry point for MetricsLoggerPlugin.
    /// Connects to kernel via named pipe IPC. 
    /// </summary>
    class Program
    {
        private static NamedPipeClientStream _pipeClient;
        private static MetricsLoggerPlugin _plugin;
        private static IpcPluginHost _host;
        private static bool _running = true;

        static void Main(string[] args)
        {
            string pipeName = null;

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--pipe" && i + 1 < args.Length)
                {
                    pipeName = args[i + 1];
                    break;
                }
            }

            if (string.IsNullOrEmpty(pipeName))
            {
                Console.Error.WriteLine("Usage: MetricsLoggerProcess --pipe <pipename>");
                Environment.Exit(1);
                return;
            }

            try
            {
                // Connect to kernel via named pipe
                _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                _pipeClient.Connect(10000); // 10 second timeout

                Console.WriteLine("Connected to kernel via named pipe.");

                // Create plugin and host
                _plugin = new MetricsLoggerPlugin();
                _host = new IpcPluginHost(_pipeClient);

                // Start message loop
                RunMessageLoop();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex. Message}");
                Environment.Exit(1);
            }
            finally
            {
                _pipeClient?.Dispose();
            }
        }

        static void RunMessageLoop()
        {
            while (_running && _pipeClient.IsConnected)
            {
                try
                {
                    var message = IpcProtocol. ReadMessage(_pipeClient);
                    if (message == null)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    HandleMessage(message);
                }
                catch (Exception ex)
                {
                    Console. Error.WriteLine($"Message loop error: {ex.Message}");
                    break;
                }
            }
        }

        static void HandleMessage(IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcMessageType.Start:
                    _plugin.Start(_host);
                    SendAck("Started");
                    break;

                case IpcMessageType. Stop:
                    _plugin.Stop();
                    SendAck("Stopped");
                    break;

                case IpcMessageType.Event:
                    if (message.Event != null)
                    {
                        _plugin.HandleEvent(message.Event);
                    }
                    break;

                case IpcMessageType. Shutdown:
                    _plugin.Stop();
                    _running = false;
                    break;
            }
        }

        static void SendAck(string response)
        {
            var ack = new IpcMessage
            {
                Type = IpcMessageType.Ack,
                Response = response
            };
            IpcProtocol. WriteMessage(_pipeClient, ack);
        }
    }

    /// <summary>
    /// IPluginHost implementation that communicates back to kernel via IPC.
    /// </summary>
    class IpcPluginHost : IPluginHost
    {
        private readonly NamedPipeClientStream _pipe;

        public IpcPluginHost(NamedPipeClientStream pipe)
        {
            _pipe = pipe;
        }

        public void Publish(EventMessage evt)
        {
            var message = new IpcMessage
            {
                Type = IpcMessageType.Publish,
                Event = evt
            };
            IpcProtocol. WriteMessage(_pipe, message);
        }

        public void Log(string message)
        {
            var ipcMessage = new IpcMessage
            {
                Type = IpcMessageType.Log,
                Response = message
            };
            IpcProtocol.WriteMessage(_pipe, ipcMessage);
        }
    }
}