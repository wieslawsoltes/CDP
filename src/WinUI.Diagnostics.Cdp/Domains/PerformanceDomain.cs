using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class PerformanceDomain
{
    private static bool _isEnabled;
    private static readonly Stopwatch _stopwatch = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                _isEnabled = true;
                _stopwatch.Restart();
                return new JsonObject();

            case "disable":
                _isEnabled = false;
                _stopwatch.Stop();
                return new JsonObject();

            case "getMetrics":
                {
                    double timestamp = _stopwatch.Elapsed.TotalSeconds;
                    long workingSet = Process.GetCurrentProcess().WorkingSet64;
                    long cpuTime = Process.GetCurrentProcess().TotalProcessorTime.Ticks;

                    var metrics = new JsonArray
                    {
                        new JsonObject { ["name"] = "Timestamp", ["value"] = timestamp },
                        new JsonObject { ["name"] = "WorkingSet", ["value"] = (double)workingSet },
                        new JsonObject { ["name"] = "CpuTime", ["value"] = (double)cpuTime / TimeSpan.TicksPerSecond }
                    };

                    return new JsonObject { ["metrics"] = metrics };
                }

            default:
                throw new Exception($"Method Performance.{action} is not implemented");
        }
    }

    public static void CleanupSession(CdpSession session)
    {
    }
}
