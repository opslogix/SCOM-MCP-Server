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
                // Direct console test
                Console.Error.WriteLine("=== MCP SERVER STARTING ===");
                Console.Error.Flush();

                // Verify Windows authentication context
                var windowsIdentity = WindowsIdentity.GetCurrent();
                Console.Error.WriteLine($"Starting SCOM MCP Server as: {windowsIdentity.Name}");
                Console.Error.Flush();

                // Get the directory where the executable is located
                string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Console.Error.WriteLine($"Running from: {exeDirectory}");
                Console.Error.Flush();

                // Load configuration from the exe directory
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(exeDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                var scomOptions = new SCOMOptions();
                configuration.GetSection("SCOM").Bind(scomOptions);

                // Initialize services
                Console.Error.WriteLine("Initializing EventLogService...");
                Console.Error.Flush();
                var eventLogService = new EventLogService();

                Console.Error.WriteLine("Initializing SCOMConnectionService...");
                Console.Error.Flush();
                var scomService = new SCOMConnectionService(eventLogService, scomOptions);

                await scomService.InitializeAsync();

                // This should definitely appear
                Console.Error.WriteLine("=== MCP SERVER READY ===");
                Console.Error.Flush();
                eventLogService.LogInformation("SCOM MCP Server initialized successfully", EventLogService.EVENT_ID_STARTUP);

                // Create and start MCP server
                var mcpServer = new MCPServer(scomService, eventLogService);
                await mcpServer.StartAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.Error.Flush();
                Environment.Exit(1);
            }
        }
    }
}