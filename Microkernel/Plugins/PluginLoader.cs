using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System. Reflection;
using Contracts;
using Microkernel.Services;

namespace Microkernel.Plugins
{
    public sealed class PluginLoader : IPluginLoader
    {
        private readonly IKernelLogger _logger;

        public PluginLoader(IKernelLogger logger)
        {
            _logger = logger ??  throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<IPlugin> LoadPlugins(string pluginsDirectory, string searchPattern, SearchOption searchOption)
        {
            if (string.IsNullOrWhiteSpace(pluginsDirectory))
            {
                throw new ArgumentException("Plugins directory must be specified.", nameof(pluginsDirectory));
            }

            var plugins = new List<IPlugin>();

            if (! Directory.Exists(pluginsDirectory))
            {
                _logger. Warn("Plugins directory does not exist: " + pluginsDirectory);
                return plugins;
            }

            // Get all DLL files, filter out obj folders and system DLLs
            var allDlls = Directory.GetFiles(pluginsDirectory, searchPattern, searchOption);
            var dllFiles = new List<string>();
            
            foreach (var dll in allDlls)
            {
                if (dll.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    continue;
                if (Path.GetFileName(dll). Equals("Contracts.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (dll.Contains(Path. DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                    dllFiles.Add(dll);
            }

            foreach (var dllPath in dllFiles)
            {
                var loadedPlugins = LoadPluginsFromAssembly(dllPath);
                plugins.AddRange(loadedPlugins);
            }

            return plugins;
        }

        private IEnumerable<IPlugin> LoadPluginsFromAssembly(string assemblyPath)
        {
            var plugins = new List<IPlugin>();
            var fileName = Path.GetFileName(assemblyPath);

            try
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly. LoadFrom(assemblyPath);
                }
                catch (BadImageFormatException)
                {
                    return plugins;
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to load assembly " + fileName + ": " + ex. Message);
                    return plugins;
                }

                Type[] types = null;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types. Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    if (! typeof(IPlugin).IsAssignableFrom(type))
                        continue;

                    if (type.GetConstructor(Type. EmptyTypes) == null)
                        continue;

                    try
                    {
                        var instance = Activator. CreateInstance(type);
                        if (instance is IPlugin plugin)
                        {
                            plugins.Add(plugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Failed to create plugin " + type.Name + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error processing " + fileName + ": " + ex.Message);
            }

            return plugins;
        }
    }
}