namespace Contracts
{
    /// <summary>
    /// Standard event topic names used across the system. 
    /// Using constants prevents typos and ensures consistency.
    /// 
    /// Naming conventions:
    /// - Business events: PascalCase (e.g., UserLoggedInEvent)
    /// - Commands/control: dot. notation (e.g., generator.start)
    /// - Alerts: dot.notation with severity (e.g., alert.warning)
    /// </summary>
    public static class EventTopics
    {
        // =============================================
        // Required Assignment Events
        // =============================================
        
        /// <summary>
        /// Event raised when a user logs in.
        /// Payload: UserLoggedInEvent (JSON)
        /// </summary>
        public const string UserLoggedIn = "UserLoggedInEvent";

        /// <summary>
        /// Event raised when data has been processed.
        /// Payload: DataProcessedEvent (JSON)
        /// </summary>
        public const string DataProcessed = "DataProcessedEvent";

        /// <summary>
        /// Event containing system metrics (CPU, RAM, Disk).
        /// Payload: SystemMetricsEvent (JSON)
        /// </summary>
        public const string SystemMetrics = "SystemMetricsEvent";

        // =============================================
        // Generator Control Commands
        // =============================================
        
        /// <summary>
        /// Command to start event generation.
        /// </summary>
        public const string GeneratorStart = "generator. start";

        /// <summary>
        /// Command to stop event generation.
        /// </summary>
        public const string GeneratorStop = "generator. stop";

        /// <summary>
        /// Command to set generation interval.
        /// Payload: interval in milliseconds (string)
        /// </summary>
        public const string GeneratorInterval = "generator.interval";

        /// <summary>
        /// Command to generate one event immediately.
        /// </summary>
        public const string GeneratorNow = "generator. now";

        // =============================================
        // Alerts
        // =============================================
        
        /// <summary>
        /// Warning-level alert (e.g., high CPU usage).
        /// </summary>
        public const string AlertWarning = "alert.warning";

        /// <summary>
        /// Critical-level alert (e.g., very high CPU usage).
        /// </summary>
        public const string AlertCritical = "alert.critical";

        // =============================================
        // Metrics
        // =============================================
        
        /// <summary>
        /// General system metrics topic.
        /// </summary>
        public const string MetricsSystem = "metrics.system";
    }
}