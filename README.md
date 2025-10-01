# SCOM MCP Server

A Model Context Protocol (MCP) server that provides programmatic access to System Center Operations Manager (SCOM) functionality through a standardized interface.

## Overview

The SCOM MCP Server enables AI assistants and other MCP clients to interact with SCOM infrastructure for monitoring, alerting, and management operations. It exposes SCOM functionality through a JSON-RPC based protocol over stdio transport.

## Features

### Alert Management
- **get_alerts** - Retrieve SCOM alerts with flexible filtering
- **count_alerts_by_severity** - Get alert counts grouped by severity (Informational, Warning, Critical)

### Agent Management
- **get_scom_agents** - List SCOM agents with filtering by computer name or management server
- **test_scom_agent** - Test agent connectivity and health status

### Management Server Operations
- **get_scom_management_servers** - List management servers in the environment
- **test_scom_management_server** - Test management server connectivity and health

### Monitoring Objects
- **get_monitoring_objects** - Query monitoring objects by class, display name, or path

### Maintenance Mode
- **set_maintenance_mode** - Put computers into maintenance mode
- **get_maintenance_mode** - Check maintenance mode status
- **stop_maintenance_mode** - Remove computers from maintenance mode

### Performance Data
- **get_performance_data** - Retrieve performance counter data for specific objects
- **get_performance_data_by_class** - Get performance data for all objects of a monitoring class
- **get_performance_statistics** - Calculate aggregated statistics (average, min, max, sum, standard deviation)
- **get_top_performance_counters** - Rank objects by performance counter values

## Prerequisites

- Windows Server with SCOM Management Server role
- .NET Framework 4.7.2 or higher
- SCOM SDK binaries installed
- Windows authentication configured for SCOM access
- Administrator or SCOM Operator permissions

## Installation

1. Clone the repository:
```bash
git clone [repository-url]
cd SCOM-MCP-Server
```

2. Ensure SCOM SDK assemblies are available at:
```
C:\Program Files\Microsoft System Center\Operations Manager\Server\SDK Binaries\
```

3. Configure `appsettings.json`:
```json
{
  "SCOM": {
    "ManagementServer": "your-scom-server.domain.com",
    "UseWindowsAuthentication": true
  }
}
```

4. Build the project:
```bash
msbuild SCOMMCPServer.csproj /p:Configuration=Release
```

## Configuration

### appsettings.json

| Setting                         | Description                           | Default  |
| ------------------------------- | ------------------------------------- | -------- |
| `SCOM.ManagementServer`         | FQDN of SCOM Management Server        | Required |
| `SCOM.UseWindowsAuthentication` | Use Windows integrated authentication | `true`   |

### Windows Event Log

The server logs to the Windows Application event log with source "SCOM-MCP-Server". Event IDs:
- 1000: Startup events
- 2000: Query operations
- 3000: Warnings
- 4000: Errors

## Usage

### Running the Server

```bash
SCOMMCPServer.exe
```

The server communicates via stdio, making it compatible with MCP clients like Claude Desktop.

### Claude Desktop Configuration

Add to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "scom": {
      "command": "C:\\path\\to\\SCOMMCPServer.exe"
    }
  }
}
```

## Tool Reference

### Alert Tools

#### get_alerts
Retrieve SCOM alerts with optional filtering.

**Parameters:**
- `filter` (string, optional): SCOM filter criteria (e.g., "Severity = 2 AND ResolutionState = 0")
- `maxResults` (integer, optional): Maximum alerts to return (default: 100)

**Example:**
```json
{
  "filter": "Severity = 2 AND ResolutionState = 0",
  "maxResults": 50
}
```

#### count_alerts_by_severity
Get count of alerts grouped by severity level.

**Parameters:** None

**Returns:** Object with counts for Informational, Warning, Critical, and Total

### Performance Tools

#### get_performance_data
Retrieve performance counter data for specific monitoring objects.

**Parameters:**
- `objectName` (string, required): Name of the monitoring object
- `counterName` (string, optional): Performance counter name (e.g., "% Processor Time")
- `startTime` (datetime, optional): Start time (default: 24 hours ago)
- `endTime` (datetime, optional): End time (default: now)
- `maxResults` (integer, optional): Maximum results (default: 1000)

#### get_performance_statistics
Calculate aggregated statistics for performance data.

**Parameters:**
- `objectName` (string, required): Monitoring object name
- `counterName` (string, required): Performance counter name
- `startTime` (datetime, optional): Start time
- `endTime` (datetime, optional): End time
- `aggregationType` (string, optional): "Average", "Min", "Max", "Sum", or "Count"

### Maintenance Mode Tools

#### set_maintenance_mode
Put a computer into maintenance mode.

**Parameters:**
- `computerName` (string, required): Computer name
- `duration` (integer, optional): Duration in minutes (default: 30)
- `reason` (string, optional): Reason for maintenance

## Architecture

### Components

- **SCOMConnectionService**: Core service handling SCOM SDK interactions
- **MCPServer**: MCP protocol implementation and request routing
- **StdioTransport**: JSON-RPC communication over stdio
- **Tools**: Individual tool implementations for each functional area
- **Models**: Data models for requests and responses

### Technology Stack

- **.NET Framework 4.7.2**: Runtime environment
- **SCOM SDK**: Microsoft.EnterpriseManagement assemblies
- **Newtonsoft.Json**: JSON serialization
- **Windows Event Log**: Operational logging

## Error Handling

The server provides detailed error messages through:
1. JSON-RPC error responses with error codes and messages
2. Windows Event Log entries for debugging
3. Console error output (stderr) for development

Common error codes:
- `-32602`: Invalid parameters
- `-32603`: Internal server error
- `-32601`: Method not found

## Security Considerations

- Runs under the context of the executing user
- Requires appropriate SCOM permissions for the service account
- Uses Windows integrated authentication by default
- No credential storage - relies on Windows security context

## Troubleshooting

### Connection Issues
1. Verify SCOM Management Server is accessible
2. Check Windows Event Log for authentication errors
3. Ensure service account has SCOM Operator role minimum

### Performance Data Issues
- Ensure monitoring objects have performance collection enabled
- Verify time range contains collected data
- Check that counter names match exactly (case-insensitive)

### Event Log
Check Windows Application log for source "SCOM-MCP-Server":
```powershell
Get-EventLog -LogName Application -Source "SCOM-MCP-Server" -Newest 20
```

## Limitations

- Windows-only (requires SCOM SDK)
- Synchronous request processing
- No built-in rate limiting
- Performance data limited by SCOM data retention policies

## Contributing

When adding new tools:
1. Create tool implementation in `Tools` folder
2. Register tool in `MCPServer.RegisterTools()`
3. Add tool definition in `MCPServer.SendToolsList()`
4. Update models as needed
5. Add appropriate error handling and logging

## License

[Your License Here]

## Support

For issues and questions:
- Check Windows Event Log for detailed error messages
- Review SCOM SDK documentation for API details
- Ensure SCOM environment is healthy and accessible