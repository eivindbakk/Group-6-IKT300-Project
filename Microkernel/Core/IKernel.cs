using System;
using System. Collections.Generic;
using Contracts;

namespace Microkernel.Core
{
    /// <summary>
    /// Core kernel interface - minimal responsibilities following microkernel pattern:
    /// - Process management (launch/stop plugins)
    /// - IPC routing (named pipes)
    /// - Message bus (pub/sub)
    /// </summary>
    public interface IKernel
    {
        // Current state of the kernel
        KernelState State { get; }

        // Lifecycle methods
        void Start();
        void Stop();

        // Pub/sub messaging
        void Publish(EventMessage message);
        IDisposable Subscribe(string topicPattern, Action<EventMessage> handler);

        // Plugin management
        IReadOnlyList<PluginInfo> GetLoadedPlugins();
        (int total, int running, int faulted) GetPluginCounts();

        // Dynamic plugin loading
        bool LoadPlugin(string pluginName, string executablePath);
        bool UnloadPlugin(string pluginName);
        bool CrashPlugin(string pluginName);
        bool RestartPlugin(string pluginName);
    }
}