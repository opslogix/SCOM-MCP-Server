using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Configuration;
using Newtonsoft.Json;
using SCOMMCPServer.Models;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    public static class SCOMAlertTools
    {
        public static async Task<string> GetAlerts(
            SCOMConnectionService scomService,
            string filter = null,
            int maxResults = 100)
        {
            try
            {
                var alerts = await Task.Run(() => scomService.GetAlerts(filter));

                var alertInfos = alerts
                    .Take(maxResults)
                    .Select(alert => new AlertInfo
                    {
                        Id = alert.Id.ToString(),
                        Name = alert.Name,
                        Description = alert.Description,
                        Severity = GetSeverityName(alert.Severity),
                        Priority = GetPriorityName(alert.Priority),
                        TimeRaised = alert.TimeRaised,
                        LastModified = alert.LastModified,
                        MonitoringObjectPath = alert.MonitoringObjectPath,
                        ResolutionState = alert.ResolutionState,
                        IsMonitorAlert = alert.IsMonitorAlert
                    })
                    .ToList();

                var result = new
                {
                    TotalCount = alerts.Count,
                    ReturnedCount = alertInfos.Count,
                    Alerts = alertInfos
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to retrieve alerts: {ex.Message}"
                });
            }
        }

        public static async Task<string> CountAlertsBySeverity(SCOMConnectionService scomService)
        {
            try
            {
                var counts = await Task.Run(() => scomService.GetAlertCountsBySeverity());

                var result = new
                {
                    Timestamp = DateTime.UtcNow,
                    AlertCounts = new
                    {
                        Total = counts.Total,
                        Critical = counts.Critical,
                        Warning = counts.Warning,
                        Informational = counts.Informational
                    },
                    Percentages = new
                    {
                        Critical = counts.Total > 0 ? $"{(counts.Critical * 100.0 / counts.Total):F2}%" : "0%",
                        Warning = counts.Total > 0 ? $"{(counts.Warning * 100.0 / counts.Total):F2}%" : "0%",
                        Informational = counts.Total > 0 ? $"{(counts.Informational * 100.0 / counts.Total):F2}%" : "0%"
                    }
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to count alerts: {ex.Message}"
                });
            }
        }

        private static string GetSeverityName(ManagementPackAlertSeverity severity)
        {
            switch (severity)
            {
                case ManagementPackAlertSeverity.Information:
                    return "Informational";
                case ManagementPackAlertSeverity.Warning:
                    return "Warning";
                case ManagementPackAlertSeverity.Error:
                    return "Critical";
                default:
                    return $"Unknown ({severity})";
            }
        }

        private static string GetPriorityName(ManagementPackWorkflowPriority priority)
        {
            switch (priority)
            {
                case ManagementPackWorkflowPriority.Low:
                    return "Low";
                case ManagementPackWorkflowPriority.Normal:
                    return "Medium";
                case ManagementPackWorkflowPriority.High:
                    return "High";
                default:
                    return $"Unknown ({priority})";
            }
        }
    }
}