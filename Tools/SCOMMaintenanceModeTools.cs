using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Monitoring;
using Newtonsoft.Json;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    public static class SCOMMaintenanceModeTools
    {
        public static async Task<string> SetMaintenanceMode(
            SCOMConnectionService scomService,
            string computerName,
            int durationMinutes,
            string reason)
        {
            try
            {
                // Input validation
                if (string.IsNullOrEmpty(computerName))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Computer name is required"
                    });
                }

                if (durationMinutes <= 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Duration must be greater than 0 minutes"
                    });
                }

                // Run the SCOM operation asynchronously
                var result = await Task.Run(() =>
                {
                    // First, get ALL objects with this display name (don't filter by class initially)
                    var allObjects = scomService.GetMonitoringObjects(
                        displayName: computerName,
                        className: null,  // Don't filter by class initially
                        objectPath: null);

                    Console.Error.WriteLine($"Found {allObjects.Count} total objects for displayName={computerName}");

                    // Then filter to Windows Computer objects
                    var computerObject = allObjects.FirstOrDefault(mo =>
                        mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                        (mo.DisplayName.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                         mo.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase)));

                    // If not found with exact match, try contains match
                    if (computerObject == null)
                    {
                        Console.Error.WriteLine("No exact match found, trying partial match...");
                        computerObject = allObjects.FirstOrDefault(mo =>
                            mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                            (mo.DisplayName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    if (computerObject == null)
                    {
                        // List what we DID find for debugging
                        var windowsComputerObjects = allObjects
                            .Where(mo => mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:"))
                            .Select(mo => new { mo.DisplayName, mo.FullName })
                            .ToList();

                        var otherObjects = allObjects
                            .Where(mo => mo.FullName == null || !mo.FullName.StartsWith("Microsoft.Windows.Computer:"))
                            .Take(5)
                            .Select(mo => new { mo.DisplayName, mo.FullName })
                            .ToList();

                        throw new InvalidOperationException(
                            $"Computer '{computerName}' not found as a Windows Computer object in SCOM. " +
                            $"Found {allObjects.Count} objects with that name, " +
                            $"including {windowsComputerObjects.Count} Windows Computer objects. " +
                            (windowsComputerObjects.Any() ?
                                $"Windows Computers found: {string.Join(", ", windowsComputerObjects.Select(o => o.DisplayName))}" :
                                $"Other objects found: {string.Join(", ", otherObjects.Select(o => $"{o.DisplayName} ({o.FullName?.Split(':')[0] ?? "Unknown Type"})"))}"));
                    }

                    Console.Error.WriteLine($"Found computer object: {computerObject.DisplayName} ({computerObject.FullName})");

                    // Schedule maintenance mode
                    DateTime startTime = DateTime.UtcNow;
                    DateTime endTime = startTime.AddMinutes(durationMinutes);

                    // Cast to PartialMonitoringObject to access maintenance mode methods
                    if (computerObject is PartialMonitoringObject partialMonitoringObject)
                    {
                        partialMonitoringObject.ScheduleMaintenanceMode(
                            startTime,
                            endTime,
                            MaintenanceModeReason.PlannedOther,
                            reason ?? "Maintenance mode set via MCP Server"
                        );

                        return new
                        {
                            Success = true,
                            ComputerName = computerName,
                            ActualDisplayName = computerObject.DisplayName,
                            MaintenanceModeScheduled = true,
                            StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                            EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                            DurationMinutes = durationMinutes,
                            Reason = reason ?? "Maintenance mode set via MCP Server"
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to cast object to PartialMonitoringObject type. Object type is: {computerObject.GetType().FullName}");
                    }
                });

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in SetMaintenanceMode: {ex}");
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to set maintenance mode: {ex.Message}"
                }, Formatting.Indented);
            }
        }

        public static async Task<string> StopMaintenanceMode(
            SCOMConnectionService scomService,
            string computerName)
        {
            try
            {
                if (string.IsNullOrEmpty(computerName))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Computer name is required"
                    });
                }

                var result = await Task.Run(() =>
                {
                    // Get ALL objects with this display name (don't filter by class initially)
                    var allObjects = scomService.GetMonitoringObjects(
                        displayName: computerName,
                        className: null,
                        objectPath: null);

                    // Filter to Windows Computer objects
                    var computerObject = allObjects.FirstOrDefault(mo =>
                        mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                        (mo.DisplayName.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                         mo.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase)));

                    if (computerObject == null)
                    {
                        computerObject = allObjects.FirstOrDefault(mo =>
                            mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                            (mo.DisplayName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    if (computerObject == null)
                    {
                        throw new InvalidOperationException($"Computer '{computerName}' not found in SCOM");
                    }

                    // Stop maintenance mode
                    if (computerObject is PartialMonitoringObject partialMonitoringObject)
                    {
                        partialMonitoringObject.StopMaintenanceMode(DateTime.UtcNow);

                        return new
                        {
                            Success = true,
                            ComputerName = computerName,
                            ActualDisplayName = computerObject.DisplayName,
                            MaintenanceModeStopped = true,
                            StoppedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to cast object to PartialMonitoringObject type. Object type is: {computerObject.GetType().FullName}");
                    }
                });

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to stop maintenance mode: {ex.Message}"
                }, Formatting.Indented);
            }
        }

        public static async Task<string> GetMaintenanceMode(
            SCOMConnectionService scomService,
            string computerName)
        {
            try
            {
                if (string.IsNullOrEmpty(computerName))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Computer name is required"
                    });
                }

                var result = await Task.Run(() =>
                {
                    // Get ALL objects with this display name (don't filter by class initially)
                    var allObjects = scomService.GetMonitoringObjects(
                        displayName: computerName,
                        className: null,
                        objectPath: null);

                    // Filter to Windows Computer objects
                    var computerObject = allObjects.FirstOrDefault(mo =>
                        mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                        (mo.DisplayName.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                         mo.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase)));

                    if (computerObject == null)
                    {
                        computerObject = allObjects.FirstOrDefault(mo =>
                            mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                            (mo.DisplayName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    if (computerObject == null)
                    {
                        throw new InvalidOperationException($"Computer '{computerName}' not found in SCOM");
                    }

                    if (computerObject is PartialMonitoringObject partialMonitoringObject)
                    {
                        var maintenanceWindow = partialMonitoringObject.GetMaintenanceWindow();

                        return new
                        {
                            Success = true,
                            ComputerName = computerName,
                            ActualDisplayName = computerObject.DisplayName,
                            InMaintenanceMode = partialMonitoringObject.InMaintenanceMode,
                            MaintenanceWindow = maintenanceWindow != null ? new
                            {
                                StartTime = maintenanceWindow.StartTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC"),
                                EndTime = maintenanceWindow.ScheduledEndTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC"),
                                Reason = maintenanceWindow.Reason.ToString(),
                                Comments = maintenanceWindow.Comments,
                                User = maintenanceWindow.User
                            } : null
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to cast object to PartialMonitoringObject type. Object type is: {computerObject.GetType().FullName}");
                    }
                });

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to get maintenance mode status: {ex.Message}"
                }, Formatting.Indented);
            }
        }

        public static async Task<string> SetMaintenanceModeWithReason(
            SCOMConnectionService scomService,
            string computerName,
            int durationMinutes,
            string reasonCode,
            string comments)
        {
            try
            {
                // Input validation
                if (string.IsNullOrEmpty(computerName))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Computer name is required"
                    });
                }

                if (durationMinutes <= 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Error = "Duration must be greater than 0 minutes"
                    });
                }

                // Parse the maintenance mode reason
                MaintenanceModeReason mmReason;
                if (!Enum.TryParse<MaintenanceModeReason>(reasonCode, true, out mmReason))
                {
                    // Default to PlannedOther if invalid reason provided
                    mmReason = MaintenanceModeReason.PlannedOther;
                }

                // Run the SCOM operation asynchronously
                var result = await Task.Run(() =>
                {
                    // Get ALL objects with this display name (don't filter by class initially)
                    var allObjects = scomService.GetMonitoringObjects(
                        displayName: computerName,
                        className: null,
                        objectPath: null);

                    // Filter to Windows Computer objects
                    var computerObject = allObjects.FirstOrDefault(mo =>
                        mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                        (mo.DisplayName.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                         mo.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase)));

                    if (computerObject == null)
                    {
                        computerObject = allObjects.FirstOrDefault(mo =>
                            mo.FullName != null && mo.FullName.StartsWith("Microsoft.Windows.Computer:") &&
                            (mo.DisplayName.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    if (computerObject == null)
                    {
                        throw new InvalidOperationException($"Computer '{computerName}' not found in SCOM");
                    }

                    // Schedule maintenance mode
                    DateTime startTime = DateTime.UtcNow;
                    DateTime endTime = startTime.AddMinutes(durationMinutes);

                    if (computerObject is PartialMonitoringObject partialMonitoringObject)
                    {
                        partialMonitoringObject.ScheduleMaintenanceMode(
                            startTime,
                            endTime,
                            mmReason,
                            comments ?? "Maintenance mode set via MCP Server"
                        );

                        return new
                        {
                            Success = true,
                            ComputerName = computerName,
                            ActualDisplayName = computerObject.DisplayName,
                            MaintenanceModeScheduled = true,
                            StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                            EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                            DurationMinutes = durationMinutes,
                            Reason = mmReason.ToString(),
                            Comments = comments ?? "Maintenance mode set via MCP Server"
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to cast object to PartialMonitoringObject type. Object type is: {computerObject.GetType().FullName}");
                    }
                });

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    ComputerName = computerName,
                    Error = $"Failed to set maintenance mode: {ex.Message}"
                }, Formatting.Indented);
            }
        }

        // Helper method to get available maintenance mode reasons
        public static async Task<string> GetMaintenanceModeReasons()
        {
            return await Task.Run(() =>
            {
                var reasons = Enum.GetValues(typeof(MaintenanceModeReason))
                    .Cast<MaintenanceModeReason>()
                    .Select(r => new
                    {
                        Code = r.ToString(),
                        Value = (int)r,
                        Description = GetReasonDescription(r)
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new
                {
                    Success = true,
                    AvailableReasons = reasons
                }, Formatting.Indented);
            });
        }

        private static string GetReasonDescription(MaintenanceModeReason reason)
        {
            switch (reason)
            {
                case MaintenanceModeReason.PlannedOther:
                    return "Planned (Other)";
                case MaintenanceModeReason.UnplannedOther:
                    return "Unplanned (Other)";
                case MaintenanceModeReason.PlannedHardwareMaintenance:
                    return "Planned Hardware Maintenance";
                case MaintenanceModeReason.UnplannedHardwareMaintenance:
                    return "Unplanned Hardware Maintenance";
                case MaintenanceModeReason.PlannedHardwareInstallation:
                    return "Planned Hardware Installation";
                case MaintenanceModeReason.UnplannedHardwareInstallation:
                    return "Unplanned Hardware Installation";
                case MaintenanceModeReason.PlannedOperatingSystemReconfiguration:
                    return "Planned Operating System Reconfiguration";
                case MaintenanceModeReason.UnplannedOperatingSystemReconfiguration:
                    return "Unplanned Operating System Reconfiguration";
                case MaintenanceModeReason.PlannedApplicationMaintenance:
                    return "Planned Application Maintenance";
                case MaintenanceModeReason.ApplicationInstallation:
                    return "Application Installation";
                case MaintenanceModeReason.ApplicationUnresponsive:
                    return "Application Unresponsive";
                case MaintenanceModeReason.ApplicationUnstable:
                    return "Application Unstable";
                case MaintenanceModeReason.SecurityIssue:
                    return "Security Issue";
                case MaintenanceModeReason.LossOfNetworkConnectivity:
                    return "Loss of Network Connectivity";
                default:
                    return reason.ToString();
            }
        }
    }
}