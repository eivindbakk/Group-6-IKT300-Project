using System;
using System.Collections.Generic;
using Contracts;
using Microkernel.Core;

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
    }
}