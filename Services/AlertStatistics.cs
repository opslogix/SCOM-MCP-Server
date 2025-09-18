using System;

namespace SCOMMCPServer.Services
{
    public class AlertStatistics
    {
        public int TotalAlerts { get; set; }
        public int NewAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public int WarningAlerts { get; set; }
        public int InformationalAlerts { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}