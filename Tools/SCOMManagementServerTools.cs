using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Administration;
using Newtonsoft.Json;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    public static class SCOMManagementServerTools
    {
        public static async Task<string> GetSCOMManagementServers(
            SCOMConnectionService scomService,
            string computerName = null,
            bool includeHealthState = true)
        {
            try
            {
                var servers = await Task.Run(() =>
                    scomService.GetSCOMManagementServers(computerName));

                var serverInfos = servers.Select(server =>
                {
                    var info = new
                    {
                        ComputerName = server.PrincipalName,
                        DisplayName = server.DisplayName,
                        Domain = server.Domain,
                        Version = server.Version?.ToString(),
                        InstallTime = server.InstallTime != DateTime.MinValue
                            ? server.InstallTime.ToString("yyyy-MM-dd HH:mm:ss")
                            : "Unknown",
                        HealthState = includeHealthState ? server.HealthState.ToString() : null,
                        IsGateway = server.IsGateway,
                        ServerType = server.IsGateway ? "Gateway Server" : "Management Server",
                        ActionAccountIdentity = server.ActionAccountIdentity,
                        HeartbeatInterval = server.HeartbeatInterval
                    };
                    return info;
                })
                .OrderBy(s => s.ComputerName)
                .ToList();

                // Calculate summary
                int totalServers = serverInfos.Count();  // Added () to invoke Count method
                int healthyServers = 0;
                int unhealthyServers = 0;
                int gatewayServers = 0;
                int managementServers = 0;

                foreach (var server in serverInfos)
                {
                    if (server.HealthState == "Success")
                        healthyServers++;
                    else if (server.HealthState != null)
                        unhealthyServers++;

                    if (server.IsGateway)
                        gatewayServers++;
                    else
                        managementServers++;
                }

                var result = new
                {
                    Success = true,
                    TotalCount = totalServers,
                    Servers = serverInfos,
                    Summary = new
                    {
                        TotalServers = totalServers,
                        HealthyServers = healthyServers,
                        UnhealthyServers = unhealthyServers,
                        GatewayServers = gatewayServers,
                        ManagementServers = managementServers
                    }
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    Error = $"Failed to retrieve management servers: {ex.Message}"
                });
            }
        }

        public static async Task<string> TestSCOMManagementServer(
            SCOMConnectionService scomService,
            string computerName)
        {
            try
            {
                if (string.IsNullOrEmpty(computerName))
                {
                    throw new ArgumentException("Computer name is required");
                }

                var isHealthy = await Task.Run(() =>
                    scomService.TestSCOMManagementServer(computerName));

                return JsonConvert.SerializeObject(new
                {
                    Success = true,
                    ComputerName = computerName,
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Management server is healthy and responding" : "Management server is unhealthy or not responding",
                    TestedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to test management server: {ex.Message}"
                });
            }
        }
    }
}