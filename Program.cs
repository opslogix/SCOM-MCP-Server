using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SCOMMCPServer.Configuration;
using SCOMMCPServer.MCP;
using SCOMMCPServer.Services;

namespace SCOMMCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Verify Windows authentication context
                var windowsIdentity = WindowsIdentity.GetCurrent();
                Console.Error.WriteLine($"Starting SCOM MCP Server as: {windowsIdentity.Name}");

                // Get the directory where the executable is located
                string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                // Load configuration from the exe directory
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(exeDirectory)  // Use exe directory instead of current directory
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                var scomOptions = new SCOMOptions();
                configuration.GetSection("SCOM").Bind(scomOptions);

                // Initialize services
                var eventLogService = new EventLogService();
                var scomService = new SCOMConnectionService(eventLogService, scomOptions);

                await scomService.InitializeAsync();

                eventLogService.LogInformation("SCOM MCP Server initialized successfully");

                // Create and start MCP server
                var mcpServer = new MCPServer(scomService, eventLogService);
                await mcpServer.StartAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}