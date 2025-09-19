using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Monitoring;
using Newtonsoft.Json;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    public static class SCOMMonitoringObjectTools
    {
        public static async Task<string> GetMonitoringObjects(
            SCOMConnectionService scomService,
            string displayName = null,
            string className = null,
            string objectPath = null,
            bool includeHealthState = true,
            int maxResults = 100)
        {
            try
            {
                var objects = await Task.Run(() =>
                    scomService.GetMonitoringObjects(displayName, className, objectPath));

                var objectInfos = objects
                    .Take(maxResults)
                    .Select(obj =>
                    {
                        var info = new
                        {
                            Id = obj.Id.ToString(),
                            DisplayName = obj.DisplayName,
                            FullName = obj.FullName,
                            Path = obj.Path,
                            ClassName = obj.GetLeastDerivedNonAbstractClass()?.Name,
                            ClassDisplayName = obj.GetLeastDerivedNonAbstractClass()?.DisplayName,
                            HealthState = includeHealthState ? obj.HealthState.ToString() : null,
                            StateLastModified = obj.StateLastModified?.ToString("yyyy-MM-dd HH:mm:ss"),
                            IsManaged = obj.IsManaged,
                            IsAvailable = obj.IsAvailable,
                            InMaintenanceMode = obj.InMaintenanceMode
                        };
                        return info;
                    })
                    .OrderBy(o => o.DisplayName)
                    .ToList();

                // Calculate summary
                int totalObjects = objectInfos.Count();
                int healthyObjects = 0;
                int warningObjects = 0;
                int criticalObjects = 0;
                int unmonitoredObjects = 0;
                int inMaintenanceMode = 0;

                foreach (var obj in objectInfos)
                {
                    if (obj.InMaintenanceMode)
                        inMaintenanceMode++;

                    switch (obj.HealthState)
                    {
                        case "Success":
                            healthyObjects++;
                            break;
                        case "Warning":
                            warningObjects++;
                            break;
                        case "Error":
                            criticalObjects++;
                            break;
                        case "Uninitialized":
                        case null:
                            unmonitoredObjects++;
                            break;
                    }
                }

                var result = new
                {
                    Success = true,
                    TotalCount = totalObjects,
                    ReturnedObjects = objectInfos,
                    Summary = new
                    {
                        TotalObjects = totalObjects,
                        HealthyObjects = healthyObjects,
                        WarningObjects = warningObjects,
                        CriticalObjects = criticalObjects,
                        UnmonitoredObjects = unmonitoredObjects,
                        InMaintenanceMode = inMaintenanceMode
                    },
                    SearchCriteria = new
                    {
                        DisplayName = displayName,
                        ClassName = className,
                        ObjectPath = objectPath
                    }
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Success = false,
                    Error = $"Failed to retrieve monitoring objects: {ex.Message}"
                });
            }
        }
    }
}