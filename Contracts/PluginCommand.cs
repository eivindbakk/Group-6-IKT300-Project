namespace Contracts
{
    /// <summary>
    /// Represents a command that a plugin handles.
    /// </summary>
    public class PluginCommand
    {
        /// <summary>
        /// The command topic (e.g., "generator.start"). 
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Description of what the command does. 
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional payload format (e.g., "<seconds>").
        /// </summary>
        public string PayloadFormat { get; set; }
    }
}