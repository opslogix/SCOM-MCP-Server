using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Administration;
using Microsoft.EnterpriseManagement.Configuration;
using Newtonsoft.Json;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    public static class SCOMAgentTools
    {
        public static async Task<string> GetSCOMAgents(
            SCOMConnectionService scomService,
            string computerName = null,
            string managementServer = null,
            bool includeHealthState = true)
        {
            try
            {
                var agents = await Task.Run(() =>
                    scomService.GetSCOMAgents(computerName, managementServer));

                var agentInfos = agents.Select(agent =>
                {
                    var info = new
                    {
                        ComputerName = agent.PrincipalName,
                        DisplayName = agent.DisplayName,
                        Domain = agent.Domain,
                        IPAddress = agent.IPAddress?.ToString(),
                        Version = agent.Version?.ToString(),
                        InstallTime = agent.InstallTime != DateTime.MinValue
                            ? agent.InstallTime.ToString("yyyy-MM-dd HH:mm:ss")
                            : "Unknown",
                        HeartbeatInterval = agent.HeartbeatInterval,
                        HealthState = includeHealthState ? agent.HealthState.ToString() : null,
                        ManagementServer = GetPrimaryManagementServerName(agent),
                        ProxyingEnabled = agent.ProxyingEnabled?.Value ?? false
                    };
                    return info;
                })
                .OrderBy(a => a.ComputerName)
                .ToList();

                // Calculate summary separately to avoid method group issues
                int totalAgents = agentInfos.Count;
                int healthyAgents = 0;
                int unhealthyAgents = 0;
                int proxyingEnabled = 0;

                foreach (var agent in agentInfos)
                {
                    if (agent.HealthState == "Success")
                        healthyAgents++;
                    else if (agent.HealthState != null)
                        unhealthyAgents++;

                    if (agent.ProxyingEnabled)
                        proxyingEnabled++;
                }

                var result = new
                {
                    Success = true,
                    TotalCount = totalAgents,
                    Agents = agentInfos,
                    Summary = new
                    {
                        TotalAgents = totalAgents,
                        HealthyAgents = healthyAgents,
                        UnhealthyAgents = unhealthyAgents,
                        ProxyingEnabled = proxyingEnabled
                    }
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    Error = $"Failed to retrieve agents: {ex.Message}"
                });
            }
        }

        public static async Task<string> TestSCOMAgent(
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
                    scomService.TestSCOMAgent(computerName));

                return JsonConvert.SerializeObject(new
                {
                    Success = true,
                    ComputerName = computerName,
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Agent is healthy and responding" : "Agent is unhealthy or not responding",
                    TestedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to test agent: {ex.Message}"
                });
            }
        }

        private static string GetPrimaryManagementServerName(AgentManagedComputer agent)
        {
            try
            {
                var ms = agent.GetPrimaryManagementServer();
                return ms?.PrincipalName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}