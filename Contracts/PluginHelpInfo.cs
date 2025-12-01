using System. Collections.Generic;

namespace Contracts
{
    /// <summary>
    /// Contains help information for a plugin. 
    /// </summary>
    public class PluginHelpInfo
    {
        /// <summary>
        /// Detailed description of the plugin. 
        /// </summary>
        public string DetailedDescription { get; set; }

        /// <summary>
        /// List of commands this plugin handles.
        /// </summary>
        public List<PluginCommand> Commands { get; set; }

        /// <summary>
        /// List of topics this plugin listens to.
        /// </summary>
        public List<string> HandledTopics { get; set; }

        /// <summary>
        /// Example usages. 
        /// </summary>
        public List<string> Examples { get; set; }

        /// <summary>
        /// Additional notes or information.
        /// </summary>
        public List<string> Notes { get; set; }

        public PluginHelpInfo()
        {
            Commands = new List<PluginCommand>();
            HandledTopics = new List<string>();
            Examples = new List<string>();
            Notes = new List<string>();
        }
    }
}