using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement;
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.Monitoring;
using SCOMMCPServer.Configuration;
using SCOMMCPServer.Models;

namespace SCOMMCPServer.Services
{
    public class SCOMConnectionService : IDisposable
    {
        private readonly EventLogService _eventLog;
        private readonly SCOMOptions _options;
        private ManagementGroup _managementGroup;

        public SCOMConnectionService(EventLogService eventLog, SCOMOptions options)
        {
            _eventLog = eventLog;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.Error.WriteLine($"Initializing SCOM connection to {_options.ManagementServer}");

                if (_options.UseWindowsAuthentication)
                {
                    var currentUser = WindowsIdentity.GetCurrent().Name;
                    Console.Error.WriteLine($"Connecting with Windows authentication as {currentUser}");
                    _eventLog.LogInformation($"SCOM connection initiated by {currentUser}");
                }

                // Connect to SCOM Management Server
                await Task.Run(() =>
                {
                    _managementGroup = new ManagementGroup(_options.ManagementServer);
                });

                if (_managementGroup.IsConnected)
                {
                    Console.Error.WriteLine("Successfully connected to SCOM Management Group");
                    _eventLog.LogInformation($"Connected to SCOM server: {_options.ManagementServer}");
                }
                else
                {
                    throw new InvalidOperationException("Failed to establish connection to SCOM");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to connect to SCOM: {ex.Message}");
                _eventLog.LogError($"SCOM connection failed: {ex.Message}", ex);
                throw;
            }
        }

        public IList<MonitoringAlert> GetAlerts(string searchFilter = null)
        {
            EnsureConnected();

            try
            {
                IList<MonitoringAlert> alerts;

                if (string.IsNullOrEmpty(searchFilter))
                {
                    // Get all new alerts
                    var criteria = new MonitoringAlertCriteria("ResolutionState = 0");
                    alerts = _managementGroup.OperationalData.GetMonitoringAlerts(criteria, null);
                }
                else
                {
                    // Apply search filter
                    var criteria = new MonitoringAlertCriteria(searchFilter);
                    alerts = _managementGroup.OperationalData.GetMonitoringAlerts(criteria, null);
                }

                Console.Error.WriteLine($"Retrieved {alerts.Count} alerts");
                _eventLog.LogInformation($"Alert query executed successfully, returned {alerts.Count} alerts");

                return alerts;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to retrieve alerts: {ex.Message}");
                _eventLog.LogError($"Alert retrieval failed: {ex.Message}", ex);
                throw;
            }
        }

        public AlertCountBySeverity GetAlertCountsBySeverity()
        {
            EnsureConnected();

            try
            {
                var counts = new AlertCountBySeverity();

                // Count Informational alerts (Severity = 0)
                var infoCriteria = new MonitoringAlertCriteria("Severity = 0 AND ResolutionState = 0");
                counts.Informational = _managementGroup.OperationalData.GetMonitoringAlertsCount(infoCriteria);

                // Count Warning alerts (Severity = 1)
                var warnCriteria = new MonitoringAlertCriteria("Severity = 1 AND ResolutionState = 0");
                counts.Warning = _managementGroup.OperationalData.GetMonitoringAlertsCount(warnCriteria);

                // Count Critical alerts (Severity = 2)
                var critCriteria = new MonitoringAlertCriteria("Severity = 2 AND ResolutionState = 0");
                counts.Critical = _managementGroup.OperationalData.GetMonitoringAlertsCount(critCriteria);

                counts.Total = counts.Informational + counts.Warning + counts.Critical;

                Console.Error.WriteLine($"Alert counts - Total: {counts.Total}, Critical: {counts.Critical}, Warning: {counts.Warning}, Info: {counts.Informational}");
                _eventLog.LogInformation($"Alert count query executed: Total={counts.Total}, Critical={counts.Critical}, Warning={counts.Warning}, Info={counts.Informational}");

                return counts;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to count alerts: {ex.Message}");
                _eventLog.LogError($"Alert counting failed: {ex.Message}", ex);
                throw;
            }
        }

        private void EnsureConnected()
        {
            if (_managementGroup == null || !_managementGroup.IsConnected)
            {
                throw new InvalidOperationException("Not connected to SCOM Management Server");
            }
        }

        public void Dispose()
        {
            _managementGroup?.Dispose();
        }
    }
}