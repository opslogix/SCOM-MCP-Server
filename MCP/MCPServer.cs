using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SCOMMCPServer.Services;
using SCOMMCPServer.Tools;

namespace SCOMMCPServer.MCP
{
    public class MCPServer
    {
        private readonly SCOMConnectionService _scomService;
        private readonly EventLogService _eventLog;
        private readonly StdioTransport _transport;
        private readonly Dictionary<string, Func<JObject, Task<object>>> _tools;

        public MCPServer(SCOMConnectionService scomService, EventLogService eventLog)
        {
            _scomService = scomService;
            _eventLog = eventLog;
            _transport = new StdioTransport();
            _tools = new Dictionary<string, Func<JObject, Task<object>>>();

            RegisterTools();
        }

        private void RegisterTools()
        {
            // Register get_alerts tool
            _tools["get_alerts"] = async (parameters) =>
            {
                string filter = parameters["filter"]?.ToString();
                int maxResults = parameters["maxResults"]?.Value<int>() ?? 100;

                return await SCOMAlertTools.GetAlerts(_scomService, filter, maxResults);
            };

            // Register count_alerts_by_severity tool
            _tools["count_alerts_by_severity"] = async (parameters) =>
            {
                return await SCOMAlertTools.CountAlertsBySeverity(_scomService);
            };

            // Register get_scom_agents tool
            _tools["get_scom_agents"] = async (parameters) =>
            {
                string computerName = parameters["computerName"]?.ToString();
                string managementServer = parameters["managementServer"]?.ToString();
                bool includeHealthState = parameters["includeHealthState"]?.Value<bool>() ?? true;

                return await SCOMAgentTools.GetSCOMAgents(
                    _scomService, computerName, managementServer, includeHealthState);
            };

            // Register test_scom_agent tool
            _tools["test_scom_agent"] = async (parameters) =>
            {
                string computerName = parameters["computerName"]?.ToString();

                return await SCOMAgentTools.TestSCOMAgent(_scomService, computerName);
            };
            _tools["get_scom_management_servers"] = async (parameters) =>
            {
                string computerName = parameters["computerName"]?.ToString();
                bool includeHealthState = parameters["includeHealthState"]?.Value<bool>() ?? true;

                return await SCOMManagementServerTools.GetSCOMManagementServers(
                    _scomService, computerName, includeHealthState);
            };

            _tools["test_scom_management_server"] = async (parameters) =>
            {
                string computerName = parameters["computerName"]?.ToString();

                return await SCOMManagementServerTools.TestSCOMManagementServer(_scomService, computerName);
            };

            _tools["get_monitoring_objects"] = async (parameters) =>
            {
                string displayName = parameters["displayName"]?.ToString();
                string className = parameters["className"]?.ToString();
                string objectPath = parameters["objectPath"]?.ToString();
                bool includeHealthState = parameters["includeHealthState"]?.Value<bool>() ?? true;
                int maxResults = parameters["maxResults"]?.Value<int>() ?? 100;

                return await SCOMMonitoringObjectTools.GetMonitoringObjects(
                    _scomService, displayName, className, objectPath, includeHealthState, maxResults);
            };
        }

        public async Task StartAsync()
        {
            _eventLog.LogInformation("MCP Server starting with stdio transport", EventLogService.EVENT_ID_STARTUP);

            // Don't send initialization immediately - wait for client's initialize request

            // Main message loop
            while (true)
            {
                try
                {
                    var message = await _transport.ReadMessageAsync();
                    if (message == null) break;

                    await ProcessMessage(message);
                }
                catch (Exception ex)
                {
                    _eventLog.LogError($"Error processing message: {ex.Message}", ex);
                    // Don't send error with null ID - just log it
                    Console.Error.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private async Task ProcessMessage(JObject message)
        {
            var method = message["method"]?.ToString();
            var id = message["id"];  // Keep as JToken, not object
            var parameters = message["params"] as JObject;

            Console.Error.WriteLine($"Received method: {method}, id: {id}");

            switch (method)
            {
                case "initialize":
                    await SendInitializeResponse(id);
                    break;

                case "tools/list":
                    await SendToolsList(id);
                    break;

                case "tools/call":
                    await CallTool(id, parameters);
                    break;

                default:
                    if (id != null)  // Only send error if there's an ID
                    {
                        await SendError(id, -32601, "Method not found", $"Unknown method: {method}");
                    }
                    break;
            }
        }

        private async Task SendInitializeResponse(JToken id)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,  // Use the same ID from the request
                ["result"] = new JObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JObject
                    {
                        ["tools"] = new JObject()
                    },
                    ["serverInfo"] = new JObject
                    {
                        ["name"] = "scom-mcp-server",
                        ["version"] = "1.0.0"
                    }
                }
            };

            await _transport.WriteMessageAsync(response);
        }

        private async Task SendToolsList(JToken id)
        {
            var tools = new JArray
            {
                new JObject
                {
                    ["name"] = "get_alerts",
                    ["description"] = "Retrieve SCOM alerts with optional search filters",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["filter"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Optional filter criteria (e.g., 'Severity = 2 AND ResolutionState = 0' for critical new alerts)"
                            },
                            ["maxResults"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Maximum number of alerts to return",
                                ["default"] = 100
                            }
                        }
                    }
                },
                new JObject
                {
                    ["name"] = "count_alerts_by_severity",
                    ["description"] = "Count SCOM alerts by severity level (Informational, Warning, Critical)",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    }
                },
                new JObject
                {
                    ["name"] = "get_scom_agents",
                    ["description"] = "Get SCOM agent information similar to Get-SCOMAgent cmdlet",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["computerName"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Optional computer name filter (partial match supported)"
                            },
                            ["managementServer"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Optional management server filter"
                            },
                            ["includeHealthState"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include agent health state information",
                                ["default"] = true
                            }
                        }
                    }
                },
                new JObject
                {
                    ["name"] = "get_monitoring_objects",
                    ["description"] = "Get SCOM monitoring objects similar to Get-SCOMMonitoringObject cmdlet",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["displayName"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Filter by display name (partial match supported)"
                            },
                            ["className"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Filter by class name (e.g., 'Microsoft.Windows.Computer', 'Microsoft.SQLServer.Database')"
                            },
                            ["objectPath"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Filter by object path (partial match supported)"
                            },
                            ["includeHealthState"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include health state information",
                                ["default"] = true
                            },
                            ["maxResults"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Maximum number of objects to return",
                                ["default"] = 100
                            }
                        }
                    }
                },
                new JObject
                {
                    ["name"] = "test_scom_agent",
                    ["description"] = "Test SCOM agent connectivity and health",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["computerName"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Computer name to test (required)"
                            }
                        },
                        ["required"] = new JArray { "computerName" }
                    }
                }
                ,
                new JObject
                {
                    ["name"] = "get_scom_management_servers",
                    ["description"] = "Get SCOM management server information similar to Get-SCOMManagementServer cmdlet",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["computerName"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Optional computer name filter (partial match supported)"
                            },
                            ["includeHealthState"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include management server health state information",
                                ["default"] = true
                            }
                        }
                    }
                },
            new JObject
            {
                ["name"] = "test_scom_management_server",
                ["description"] = "Test SCOM management server connectivity and health",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["computerName"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Management server computer name to test (required)"
                        }
                    },
                    ["required"] = new JArray { "computerName" }
                }
            }

            };

            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JObject
                {
                    ["tools"] = tools
                }
            };

            await _transport.WriteMessageAsync(response);
        }

        private async Task CallTool(JToken id, JObject parameters)
        {
            try
            {
                var toolName = parameters["name"]?.ToString();
                var toolArgs = parameters["arguments"] as JObject ?? new JObject();

                if (!_tools.ContainsKey(toolName))
                {
                    await SendError(id, -32602, "Invalid params", $"Unknown tool: {toolName}");
                    return;
                }

                var result = await _tools[toolName](toolArgs);

                var response = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = new JObject
                    {
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "text",
                                ["text"] = result.ToString()
                            }
                        }
                    }
                };

                await _transport.WriteMessageAsync(response);
            }
            catch (Exception ex)
            {
                _eventLog.LogError($"Tool execution failed: {ex.Message}", ex);
                await SendError(id, -32603, "Internal error", ex.Message);
            }
        }

        private async Task SendError(JToken id, int code, string message, string data = null)
        {
            var error = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                    ["data"] = data
                }
            };

            await _transport.WriteMessageAsync(error);
        }
    }
}