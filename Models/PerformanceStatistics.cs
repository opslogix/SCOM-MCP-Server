using System;

namespace SCOMMCPServer.Models
{
    /// <summary>
    /// Performance statistics model
    /// </summary>
    public class PerformanceStatistics
    {
        public string ObjectName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int SampleCount { get; set; }
        public AggregationType AggregationType { get; set; }
        public double Value { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double StandardDeviation { get; set; }
    }
}