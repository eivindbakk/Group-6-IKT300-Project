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
        public const string UserLoggedIn = "UserLoggedInEvent";
        public const string DataProcessed = "DataProcessedEvent";
        public const string SystemMetrics = "SystemMetricsEvent";

        // =============================================
        // Generator Control Commands
        // These topics control the EventGenerator plugin
        // =============================================
        
        public const string GeneratorStart = "generator. start";
        public const string GeneratorStop = "generator.stop";
        public const string GeneratorInterval = "generator.interval";
        public const string GeneratorNow = "generator. now";

        // =============================================
        // Alerts - published when thresholds exceeded
        // =============================================
        
        public const string AlertWarning = "alert. warning";
        public const string AlertCritical = "alert. critical";

    }
}