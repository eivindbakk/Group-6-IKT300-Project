using System;

namespace Contracts.Events
{
    public class SystemMetricsEvent
    {
        public string MachineName { get; set; }
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public DateTime Timestamp { get; set; }
    }
}