using System;

namespace SCOMMCPServer.Models
{
    public class AlertInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime TimeRaised { get; set; }
        public DateTime LastModified { get; set; }
        public string MonitoringObjectPath { get; set; } = string.Empty;
        public byte ResolutionState { get; set; }
        public bool IsMonitorAlert { get; set; }
    }
}