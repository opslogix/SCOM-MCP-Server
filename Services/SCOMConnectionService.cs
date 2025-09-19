using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement;
using Microsoft.EnterpriseManagement.Administration;
using Microsoft.EnterpriseManagement.Common;
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
                    _eventLog.LogInformation($"SCOM connection initiated by {currentUser}", EventLogService.EVENT_ID_STARTUP);
                }

                // Connect to SCOM Management Server
                await Task.Run(() =>
                {
                    _managementGroup = new ManagementGroup(_options.ManagementServer);
                });

                if (_managementGroup.IsConnected)
                {
                    Console.Error.WriteLine("Successfully connected to SCOM Management Group");
                    _eventLog.LogInformation($"Connected to SCOM server: {_options.ManagementServer}", EventLogService.EVENT_ID_STARTUP);
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

            var stopwatch = Stopwatch.StartNew();

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

                stopwatch.Stop();

                Console.Error.WriteLine($"Retrieved {alerts.Count} alerts in {stopwatch.ElapsedMilliseconds}ms");
                _eventLog.LogQuery("Alert query", alerts.Count, stopwatch.ElapsedMilliseconds);

                return alerts;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to retrieve alerts after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Alert retrieval failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        public AlertCountBySeverity GetAlertCountsBySeverity()
        {
            EnsureConnected();

            var stopwatch = Stopwatch.StartNew();

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

                stopwatch.Stop();

                Console.Error.WriteLine($"Alert counts - Total: {counts.Total}, Critical: {counts.Critical}, Warning: {counts.Warning}, Info: {counts.Informational} - Retrieved in {stopwatch.ElapsedMilliseconds}ms");
                _eventLog.LogQuery($"Alert count query (Total={counts.Total}, Critical={counts.Critical}, Warning={counts.Warning}, Info={counts.Informational})",
                                  counts.Total, stopwatch.ElapsedMilliseconds);

                return counts;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to count alerts after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Alert counting failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        public IList<PartialMonitoringObject> GetMonitoringObjects(
            string displayName = null,
            string className = null,
            string objectPath = null)
        {
            EnsureConnected();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                List<PartialMonitoringObject> objects = new List<PartialMonitoringObject>();

                if (!string.IsNullOrEmpty(className))
                {
                    // Get ALL classes matching the pattern (not just FirstOrDefault!)
                    var monitoringClasses = _managementGroup.EntityTypes.GetClasses(
                        new ManagementPackClassCriteria($"Name LIKE '%{className}%'"));

                    Console.Error.WriteLine($"Found {monitoringClasses.Count()} matching classes for pattern '{className}'");

                    foreach (var monitoringClass in monitoringClasses)
                    {
                        // Skip abstract classes - they don't have instances
                        if (monitoringClass.Abstract)
                        {
                            Console.Error.WriteLine($"  Skipping abstract class: {monitoringClass.Name}");
                            continue;
                        }

                        try
                        {
                            Console.Error.WriteLine($"  Getting objects from class: {monitoringClass.Name}");
                            var reader = _managementGroup.EntityObjects.GetObjectReader<PartialMonitoringObject>(
                                monitoringClass, ObjectQueryOptions.Default);
                            var classObjects = reader.ToList();

                            if (classObjects.Count > 0)
                            {
                                Console.Error.WriteLine($"    Found {classObjects.Count} objects in {monitoringClass.Name}");
                                objects.AddRange(classObjects);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"    Error reading from {monitoringClass.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // NO CLASS SPECIFIED - GET ALL MONITORING OBJECTS!
                    Console.Error.WriteLine("No class filter specified - retrieving ALL monitoring objects from SCOM...");

                    // Get ALL objects by enumerating all non-abstract classes
                    objects = GetAllObjectsFromAllClasses();
                }

                // Apply display name filter if specified
                if (!string.IsNullOrEmpty(displayName))
                {
                    var beforeCount = objects.Count;
                    objects = objects.Where(o =>
                        o.DisplayName != null &&
                        o.DisplayName.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    Console.Error.WriteLine($"Display name filter '{displayName}' reduced objects from {beforeCount} to {objects.Count}");
                }

                // Apply path filter if specified
                if (!string.IsNullOrEmpty(objectPath))
                {
                    var beforeCount = objects.Count;
                    objects = objects.Where(o =>
                        o.Path != null &&
                        o.Path.IndexOf(objectPath, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    Console.Error.WriteLine($"Path filter '{objectPath}' reduced objects from {beforeCount} to {objects.Count}");
                }

                stopwatch.Stop();
                Console.Error.WriteLine($"=== Final Result: Retrieved {objects.Count} monitoring objects in {stopwatch.ElapsedMilliseconds}ms ===");
                _eventLog.LogQuery($"Monitoring object query", objects.Count, stopwatch.ElapsedMilliseconds);

                return objects;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to retrieve monitoring objects after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Monitoring object retrieval failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        private List<PartialMonitoringObject> GetAllObjectsFromAllClasses()
        {
            var allObjects = new Dictionary<Guid, PartialMonitoringObject>();

            try
            {
                Console.Error.WriteLine("Enumerating all classes to get all monitoring objects...");

                // Get ALL classes in the management group - use a very broad criteria
                var allClasses = new List<ManagementPackClass>();

                try
                {
                    // Try to get all classes with a broad search
                    allClasses = _managementGroup.EntityTypes.GetClasses(
                        new ManagementPackClassCriteria("Id != ''")).ToList();
                }
                catch
                {
                    // If that fails, get classes from each management pack
                    Console.Error.WriteLine("Broad class search failed, enumerating management packs...");
                    var allMPs = _managementGroup.ManagementPacks.GetManagementPacks();

                    foreach (var mp in allMPs)
                    {
                        try
                        {
                            var mpClasses = mp.GetClasses();
                            allClasses.AddRange(mpClasses);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to get classes from MP '{mp.Name}': {ex.Message}");
                        }
                    }
                }

                // Remove duplicates
                var uniqueClasses = allClasses.GroupBy(c => c.Id).Select(g => g.First()).ToList();

                Console.Error.WriteLine($"Found {uniqueClasses.Count} unique classes to process");

                int processedCount = 0;
                int skippedAbstract = 0;
                int successfulClasses = 0;

                foreach (var mpClass in uniqueClasses)
                {
                    processedCount++;

                    // Skip abstract classes
                    if (mpClass.Abstract)
                    {
                        skippedAbstract++;
                        continue;
                    }

                    try
                    {
                        var reader = _managementGroup.EntityObjects.GetObjectReader<PartialMonitoringObject>(
                            mpClass, ObjectQueryOptions.Default);

                        var classObjects = reader.ToList();
                        if (classObjects.Count > 0)
                        {
                            successfulClasses++;
                            Console.Error.WriteLine($"[{processedCount}/{uniqueClasses.Count}] {mpClass.Name}: {classObjects.Count} objects");

                            foreach (var obj in classObjects)
                            {
                                // Use dictionary to avoid duplicates
                                if (!allObjects.ContainsKey(obj.Id))
                                {
                                    allObjects[obj.Id] = obj;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Some classes might not be readable, continue with others
                    }

                    // Progress update every 25 classes
                    if (processedCount % 25 == 0)
                    {
                        Console.Error.WriteLine($"Progress: {processedCount}/{uniqueClasses.Count} classes processed, {allObjects.Count} unique objects found");
                    }
                }

                Console.Error.WriteLine($"Enumeration complete: Processed {processedCount} classes (skipped {skippedAbstract} abstract), found objects in {successfulClasses} classes");
                Console.Error.WriteLine($"Total unique objects found: {allObjects.Count}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to enumerate classes: {ex.Message}");
            }

            return allObjects.Values.ToList();
        }

        // Optional: Overloaded method for natural language queries
        public IList<PartialMonitoringObject> GetMonitoringObjects(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return GetMonitoringObjects(null, null, null);
            }

            var lowerQuery = query.ToLower();

            // Check for "get all" without any filters
            if (lowerQuery.Contains("get all") && lowerQuery.Contains("object"))
            {
                Console.Error.WriteLine("Interpreting query as: Get ALL monitoring objects");
                return GetMonitoringObjects(null, null, null);
            }

            // Parse for specific patterns
            if (lowerQuery.Contains("sql") && lowerQuery.Contains("database"))
            {
                Console.Error.WriteLine("Interpreting query as: Get SQL Database objects");
                return GetMonitoringObjects(null, "SQLServer.Database", null);
            }

            if (lowerQuery.Contains("windows") && lowerQuery.Contains("computer"))
            {
                Console.Error.WriteLine("Interpreting query as: Get Windows Computer objects");
                return GetMonitoringObjects(null, "Microsoft.Windows.Computer", null);
            }

            if (lowerQuery.Contains("sql"))
            {
                Console.Error.WriteLine("Interpreting query as: Get SQL-related objects");
                return GetMonitoringObjects(null, "SQL", null);
            }

            if (lowerQuery.Contains("iis") || lowerQuery.Contains("web"))
            {
                Console.Error.WriteLine("Interpreting query as: Get IIS/Web objects");
                return GetMonitoringObjects(null, "IIS", null);
            }

            if (lowerQuery.Contains("exchange"))
            {
                Console.Error.WriteLine("Interpreting query as: Get Exchange objects");
                return GetMonitoringObjects(null, "Exchange", null);
            }

            if (lowerQuery.Contains("active directory") || lowerQuery.Contains("domain controller"))
            {
                Console.Error.WriteLine("Interpreting query as: Get AD objects");
                return GetMonitoringObjects(null, "AD", null);
            }

            // Default: treat the query as a class name search
            Console.Error.WriteLine($"Interpreting query as class name search for: {query}");
            return GetMonitoringObjects(null, query, null);
        }

        public IList<AgentManagedComputer> GetSCOMAgents(
            string computerName = null,
            string managementServer = null)
        {
            EnsureConnected();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Get all agent-managed computers
                var allAgents = _managementGroup.Administration.GetAllAgentManagedComputers();

                IList<AgentManagedComputer> filteredAgents = allAgents;

                // Filter by computer name if specified
                if (!string.IsNullOrEmpty(computerName))
                {
                    filteredAgents = allAgents
                        .Where(a => a.PrincipalName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                // Filter by management server if specified
                if (!string.IsNullOrEmpty(managementServer))
                {
                    filteredAgents = filteredAgents
                        .Where(a =>
                        {
                            // Check management server name
                            var msName = a.GetPrimaryManagementServer()?.PrincipalName ?? "";
                            return msName.IndexOf(managementServer, StringComparison.OrdinalIgnoreCase) >= 0;
                        })
                        .ToList();
                }

                stopwatch.Stop();

                Console.Error.WriteLine($"Retrieved {filteredAgents.Count} agents in {stopwatch.ElapsedMilliseconds}ms");
                _eventLog.LogQuery($"Agent query", filteredAgents.Count, stopwatch.ElapsedMilliseconds);

                return filteredAgents;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to retrieve agents after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Agent retrieval failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        public IList<ManagementServer> GetSCOMManagementServers(
            string computerName = null)
        {
            EnsureConnected();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Get all management servers
                var allServers = _managementGroup.Administration.GetAllManagementServers();

                IList<ManagementServer> filteredServers = allServers;

                // Filter by computer name if specified
                if (!string.IsNullOrEmpty(computerName))
                {
                    filteredServers = allServers
                        .Where(s => s.PrincipalName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                stopwatch.Stop();

                Console.Error.WriteLine($"Retrieved {filteredServers.Count} management servers in {stopwatch.ElapsedMilliseconds}ms");
                _eventLog.LogQuery($"Management server query", filteredServers.Count, stopwatch.ElapsedMilliseconds);

                return filteredServers;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to retrieve management servers after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Management server retrieval failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        public bool TestSCOMManagementServer(string computerName)
        {
            EnsureConnected();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var servers = _managementGroup.Administration.GetAllManagementServers();
                var server = servers.FirstOrDefault(s =>
                    string.Equals(s.PrincipalName, computerName, StringComparison.OrdinalIgnoreCase));

                if (server == null)
                {
                    throw new InvalidOperationException($"Management server {computerName} not found");
                }

                stopwatch.Stop();

                bool isHealthy = server.HealthState == HealthState.Success;
                bool isGateway = server.IsGateway;

                string serverType = isGateway ? "Gateway Server" : "Management Server";

                Console.Error.WriteLine($"Management server {computerName} test completed in {stopwatch.ElapsedMilliseconds}ms - Type: {serverType}, Healthy: {isHealthy}");
                _eventLog.LogQuery($"Management server test for {computerName}", 1, stopwatch.ElapsedMilliseconds);

                return isHealthy;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to test management server after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Management server test failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
                throw;
            }
        }

        public bool TestSCOMAgent(string computerName)
        {
            EnsureConnected();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var agents = _managementGroup.Administration.GetAllAgentManagedComputers();
                var agent = agents.FirstOrDefault(a =>
                    string.Equals(a.PrincipalName, computerName, StringComparison.OrdinalIgnoreCase));

                if (agent == null)
                {
                    throw new InvalidOperationException($"Agent {computerName} not found");
                }

                stopwatch.Stop();

                bool isHealthy = agent.HealthState == HealthState.Success;
                string status = isHealthy ? "Healthy" : $"Unhealthy ({agent.HealthState})";

                // Get additional agent information that's actually available
                string version = agent.Version != null ? agent.Version.ToString() : "Unknown";
                DateTime? installTime = agent.InstallTime;

                Console.Error.WriteLine($"Agent {computerName} test completed in {stopwatch.ElapsedMilliseconds}ms - Status: {status}, Version: {version}");
                _eventLog.LogQuery($"Agent test for {computerName}", 1, stopwatch.ElapsedMilliseconds);

                return isHealthy;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"Failed to test agent after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                _eventLog.LogError($"Agent test failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}", ex);
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