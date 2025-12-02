using System;
using System.Collections.Generic;
using Contracts;

namespace Microkernel.Core
{
    public interface IKernel : IDisposable
    {
        KernelState State { get; }
        void Start();
        void Stop();
        void Publish(EventMessage message);
        IReadOnlyList<PluginInfo> GetLoadedPlugins();
        IPlugin GetPlugin(string name);
        IDisposable Subscribe(string topicPattern, Action<EventMessage> handler);
        bool UnloadPlugin(string pluginName);
        IReadOnlyList<string> GetAutoSubscriptions();
        
        // Forcefully crash a plugin - actually stops it and corrupts its state
        bool CrashPlugin(string pluginName);
        
        // Restart a faulted plugin
        bool RestartPlugin(string pluginName);
    }
}