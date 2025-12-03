using System;

namespace Contracts.Events
{
    /// <summary>
    /// Event raised when data has been processed (required by assignment).  
    /// </summary>
    public class DataProcessedEvent
    {
        // Unique identifier for this processing operation
        public string ProcessId { get; set; }
        
        // Name of the data source (e.g., "CustomerDB", "OrdersDB")
        public string DataSource { get; set; }
        
        // Number of records that were processed
        public int RecordsProcessed { get; set; }
        
        // UTC timestamp when processing completed
        public DateTime ProcessedAt { get; set; }
        
        // How long the processing took in milliseconds
        public double ProcessingTimeMs { get; set; }
        
        // Whether the processing succeeded or failed
        public bool Success { get; set; }
        
        // Error message if Success is false
        public string ErrorMessage { get; set; }

        public DataProcessedEvent()
        {
            // Auto-generate a unique ID and set defaults
            ProcessId = Guid.NewGuid().ToString();
            ProcessedAt = DateTime. UtcNow;
            Success = true;
        }
    }
}