using System;

namespace Contracts.Events
{
    /// <summary>
    /// Event raised when data has been processed (required by assignment). 
    /// </summary>
    public class DataProcessedEvent
    {
        public string ProcessId { get; set; }
        public string DataSource { get; set; }
        public int RecordsProcessed { get; set; }
        public DateTime ProcessedAt { get; set; }
        public double ProcessingTimeMs { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public DataProcessedEvent()
        {
            ProcessId = Guid.NewGuid().ToString();
            ProcessedAt = DateTime.UtcNow;
            Success = true;
        }
    }
}