using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class SystemInfoDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getInfo":
                {
                    var gpuDevices = new JsonArray
                    {
                        new JsonObject
                        {
                            ["vendorId"] = 0,
                            ["deviceId"] = 0,
                            ["vendorString"] = "Unknown",
                            ["deviceString"] = "Unknown",
                            ["driverVendor"] = "Unknown",
                            ["driverVersion"] = "Unknown"
                        }
                    };

                    var gpuInfo = new JsonObject
                    {
                        ["devices"] = gpuDevices,
                        ["driverBugWorkarounds"] = new JsonArray()
                    };

                    return new JsonObject
                    {
                        ["gpu"] = gpuInfo,
                        ["modelName"] = RuntimeInformation.OSDescription,
                        ["modelVersion"] = RuntimeInformation.OSArchitecture.ToString(),
                        ["commandLine"] = Environment.CommandLine
                    };
                }

            case "getProcessInfo":
                {
                    using var process = Process.GetCurrentProcess();
                    
                    var processObj = new JsonObject
                    {
                        ["id"] = process.Id,
                        ["type"] = "browser",
                        ["cpuTime"] = process.TotalProcessorTime.TotalSeconds,
                        ["workingSet"] = process.WorkingSet64,
                        ["threadCount"] = process.Threads.Count
                    };

                    var processInfoArray = new JsonArray { processObj };

                    return new JsonObject
                    {
                        ["processInfo"] = processInfoArray
                    };
                }

            case "getFeatureState":
                return new JsonObject
                {
                    ["featureEnabled"] = false
                };

            default:
                throw new Exception($"Method SystemInfo.{action} is not implemented");
        }
    }
}
