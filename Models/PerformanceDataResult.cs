using System;

namespace SCOMMCPServer.Models
{
    /// <summary>
    /// Performance data result model
    /// </summary>
    public class PerformanceDataResult
    {
        public string ObjectName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public double SampleValue { get; set; }
        public DateTime TimeSampled { get; set; }
        public DateTime TimeAdded { get; set; }
        public string RuleDisplayName { get; set; } = string.Empty;
        public string MonitoringObjectPath { get; set; } = string.Empty;
        public Guid MonitoringObjectId { get; set; }
    }
}
