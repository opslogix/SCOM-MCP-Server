using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Monitoring;
using Newtonsoft.Json;
using SCOMMCPServer.Models;
using SCOMMCPServer.Services;

namespace SCOMMCPServer.Tools
{
    /// <summary>
    /// Tool for extracting performance data from SCOM
    /// </summary>
    public static class SCOMPerformanceTools
    {
        /// <summary>
        /// Get performance data for monitoring objects
        /// </summary>
        public static async Task<string> GetPerformanceData(
            SCOMConnectionService scomService,
            string objectName,
            string counterName = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            int maxResults = 1000)
        {
            try
            {
                var start = startTime ?? DateTime.UtcNow.AddHours(-24);
                var end = endTime ?? DateTime.UtcNow;

                var performanceData = await Task.Run(() =>
                    scomService.GetPerformanceData(objectName, counterName, start, end));

                var results = performanceData
                    .Take(maxResults)
                    .Select(pd => new PerformanceDataResult
                    {
                        ObjectName = pd.ObjectName,
                        CounterName = pd.CounterName,
                        InstanceName = pd.InstanceName,
                        SampleValue = pd.SampleValue,
                        TimeSampled = pd.TimeSampled,
                        TimeAdded = pd.TimeAdded,
                        RuleDisplayName = pd.RuleDisplayName,
                        MonitoringObjectPath = pd.MonitoringObjectPath
                    })
                    .ToList();

                var response = new
                {
                    TimeRange = new
                    {
                        StartTime = start,
                        EndTime = end
                    },
                    TotalSamples = performanceData.Count,
                    ReturnedSamples = results.Count,
                    Data = results
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to retrieve performance data: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get performance data by monitoring class
        /// </summary>
        public static async Task<string> GetPerformanceDataByClass(
            SCOMConnectionService scomService,
            string className,
            string counterName = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            int maxResults = 1000)
        {
            try
            {
                var start = startTime ?? DateTime.UtcNow.AddHours(-24);
                var end = endTime ?? DateTime.UtcNow;

                var performanceData = await Task.Run(() =>
                    scomService.GetPerformanceDataByClass(className, counterName, start, end));

                var groupedData = performanceData
                    .Take(maxResults)
                    .GroupBy(pd => new { pd.ObjectName, pd.CounterName })
                    .Select(g => new
                    {
                        ObjectName = g.Key.ObjectName,
                        CounterName = g.Key.CounterName,
                        SampleCount = g.Count(),
                        AverageValue = g.Average(pd => pd.SampleValue),
                        MinValue = g.Min(pd => pd.SampleValue),
                        MaxValue = g.Max(pd => pd.SampleValue),
                        LastValue = g.OrderByDescending(pd => pd.TimeSampled).First().SampleValue,
                        Samples = g.Select(pd => new
                        {
                            Value = pd.SampleValue,
                            Time = pd.TimeSampled
                        }).ToList()
                    })
                    .ToList();

                var response = new
                {
                    ClassName = className,
                    TimeRange = new
                    {
                        StartTime = start,
                        EndTime = end
                    },
                    ObjectCount = groupedData.Count,
                    PerformanceData = groupedData
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to retrieve performance data by class: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get aggregated performance statistics
        /// </summary>
        public static async Task<string> GetPerformanceStatistics(
            SCOMConnectionService scomService,
            string objectName,
            string counterName,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string aggregationType = "Average")
        {
            try
            {
                var start = startTime ?? DateTime.UtcNow.AddHours(-24);
                var end = endTime ?? DateTime.UtcNow;

                var stats = await Task.Run(() =>
                    scomService.GetPerformanceStatistics(
                        objectName, counterName, start, end,
                        ParseAggregationType(aggregationType)));

                var response = new
                {
                    ObjectName = stats.ObjectName,
                    CounterName = stats.CounterName,
                    InstanceName = stats.InstanceName,
                    TimeRange = new
                    {
                        StartTime = stats.StartTime,
                        EndTime = stats.EndTime
                    },
                    Statistics = new
                    {
                        AggregationType = stats.AggregationType.ToString(),
                        Value = stats.Value,
                        SampleCount = stats.SampleCount,
                        Average = stats.Average,
                        Min = stats.Min,
                        Max = stats.Max,
                        StandardDeviation = stats.StandardDeviation
                    }
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to retrieve performance statistics: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get top N performance counters by value
        /// </summary>
        public static async Task<string> GetTopPerformanceCounters(
            SCOMConnectionService scomService,
            string className,
            string counterName,
            int topN = 10,
            DateTime? startTime = null,
            DateTime? endTime = null)
        {
            try
            {
                var start = startTime ?? DateTime.UtcNow.AddHours(-24);
                var end = endTime ?? DateTime.UtcNow;

                var performanceData = await Task.Run(() =>
                    scomService.GetPerformanceDataByClass(className, counterName, start, end));

                var topCounters = performanceData
                    .GroupBy(pd => pd.ObjectName)
                    .Select(g => new
                    {
                        ObjectName = g.Key,
                        AverageValue = g.Average(pd => pd.SampleValue),
                        MaxValue = g.Max(pd => pd.SampleValue),
                        MinValue = g.Min(pd => pd.SampleValue),
                        LastValue = g.OrderByDescending(pd => pd.TimeSampled).First().SampleValue,
                        SampleCount = g.Count()
                    })
                    .OrderByDescending(x => x.AverageValue)
                    .Take(topN)
                    .ToList();

                var response = new
                {
                    ClassName = className,
                    CounterName = counterName,
                    TimeRange = new
                    {
                        StartTime = start,
                        EndTime = end
                    },
                    TopN = topN,
                    Results = topCounters.Select((counter, index) => new
                    {
                        Rank = index + 1,
                        ObjectName = counter.ObjectName,
                        AverageValue = Math.Round(counter.AverageValue, 2),
                        MaxValue = Math.Round(counter.MaxValue, 2),
                        MinValue = Math.Round(counter.MinValue, 2),
                        LastValue = Math.Round(counter.LastValue, 2),
                        SampleCount = counter.SampleCount
                    }).ToList()
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    Error = $"Failed to retrieve top performance counters: {ex.Message}"
                });
            }
        }

        private static AggregationType ParseAggregationType(string type)
        {
            if (Enum.TryParse<AggregationType>(type, true, out var result))
            {
                return result;
            }
            return AggregationType.Average;
        }
    }
}