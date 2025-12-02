using System;
using System.Collections. Generic;
using Contracts;

namespace Microkernel.Core
{
    /// <summary>
    /// Core kernel interface - minimal responsibilities:
    /// - Process management
    /// - IPC routing
    /// - Message bus
    /// </summary>
    public interface IKernel
    {
        KernelState State { get; }

        void Start();
        void Stop();

        void Publish(EventMessage message);
        IDisposable Subscribe(string topicPattern, Action<EventMessage> handler);

        IReadOnlyList<PluginInfo> GetLoadedPlugins();
        (int total, int running, int faulted) GetPluginCounts();

        bool LoadPlugin(string pluginName, string executablePath);
        bool UnloadPlugin(string pluginName);
        bool CrashPlugin(string pluginName);
        bool RestartPlugin(string pluginName);
    }
}