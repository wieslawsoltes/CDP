using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Chrome.DevTools.Protocol.Domains;

public static class ProfilerDomain
{
    private static readonly ConcurrentDictionary<CdpSession, ProfilerState> _states = new();

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                _states.GetOrAdd(session, s => new ProfilerState());
                return Task.FromResult(new JsonObject());

            case "disable":
                if (_states.TryRemove(session, out var stateToDisable))
                {
                    stateToDisable.Stop();
                }
                return Task.FromResult(new JsonObject());

            case "start":
                {
                    var state = _states.GetOrAdd(session, s => new ProfilerState());
                    state.Start();
                    return Task.FromResult(new JsonObject());
                }

            case "stop":
                {
                    if (_states.TryGetValue(session, out var state))
                    {
                        var profile = state.Stop();
                        return Task.FromResult(new JsonObject
                        {
                            ["profile"] = profile
                        });
                    }
                    throw new Exception("Profiler has not been started");
                }

            default:
                throw new Exception($"Method Profiler.{action} is not implemented");
        }
    }

    public static void CleanupSession(CdpSession session)
    {
        if (_states.TryRemove(session, out var state))
        {
            state.Stop();
        }
    }

    public static void RecordActivity(CdpSession session, string name, DateTime startTime, DateTime endTime)
    {
        if (_states.TryGetValue(session, out var state) && state.IsRunning)
        {
            state.AddSpan(new ProfileSpan
            {
                Name = name,
                StartTime = startTime,
                EndTime = endTime
            });
        }
    }
}

public struct ProfileSpan
{
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class ProfilerState
{
    private readonly object _lock = new();
    private readonly List<ProfileSpan> _spans = new();
    private DateTime _startTime;
    private bool _isRunning;

    // Real-time EventPipe profiling session details
    private EventPipeSession? _session;
    private string? _tempNetTraceFile;
    private string? _tempEtlxFile;
    private Task? _copyTask;

    public bool IsRunning => _isRunning;

    public void Start()
    {
        lock (_lock)
        {
            _spans.Clear();
            _startTime = DateTime.UtcNow;
            _isRunning = true;

            try
            {
                // Start a real EventPipe session targeting the current process
                _tempNetTraceFile = Path.Combine(Path.GetTempPath(), $"cdp_profile_{Guid.NewGuid()}.nettrace");
                var client = new DiagnosticsClient(System.Diagnostics.Process.GetCurrentProcess().Id);
                var providers = new List<EventPipeProvider>
                {
                    new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", System.Diagnostics.Tracing.EventLevel.Verbose),
                    new EventPipeProvider("Microsoft-Windows-DotNETRuntime", System.Diagnostics.Tracing.EventLevel.Verbose, 0x80000002019L),
                    new EventPipeProvider("Microsoft-Windows-DotNETRuntimeRundown", System.Diagnostics.Tracing.EventLevel.Verbose, 0x2058)
                };

                _session = client.StartEventPipeSession(providers, requestRundown: true);
                var stream = _session.EventStream;
                var filePath = _tempNetTraceFile;

                 _copyTask = Task.Run(async () =>
                {
                    try
                    {
                        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                        byte[] buffer = new byte[16 * 1024];
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                    catch (Exception ex)
                    {
                        CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] Copy stream task failed: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Fallback to simulated profiling if EventPipe fails to launch (e.g. lacks privileges or dependencies)
                CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] Failed to initialize EventPipe profiling: {ex.Message}");
                _session = null;
                _tempNetTraceFile = null;
            }
        }
    }

    public void AddSpan(ProfileSpan span)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _spans.Add(span);
            }
        }
    }

    public JsonObject Stop()
    {
        DateTime endTime;
        List<ProfileSpan> spansCopy;
        DateTime startTimeCopy;
        EventPipeSession? sessionToStop;
        Task? copyTaskToWait;
        string? netTraceFileToProcess;

        lock (_lock)
        {
            endTime = DateTime.UtcNow;
            _isRunning = false;
            spansCopy = new List<ProfileSpan>(_spans);
            startTimeCopy = _startTime;
            sessionToStop = _session;
            copyTaskToWait = _copyTask;
            netTraceFileToProcess = _tempNetTraceFile;

            _session = null;
            _copyTask = null;
            _tempNetTraceFile = null;
        }

        if (sessionToStop != null)
        {
            try
            {
                sessionToStop.Stop();
            }
            catch {}

            if (copyTaskToWait != null)
            {
                try
                {
                    if (!copyTaskToWait.Wait(15000))
                    {
                        CdpServer.OriginalOut.WriteLine("[CDP PROFILER] Warning: copy task did not finish in 15 seconds.");
                    }
                }
                catch (Exception ex)
                {
                    CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] Copy task wait threw exception: {ex}");
                }
            }
        }

        if (netTraceFileToProcess != null && File.Exists(netTraceFileToProcess))
        {
            try
            {
                var profile = ConvertNetTraceToV8Profile(netTraceFileToProcess);
                var samples = profile["samples"]?.AsArray();
                if (samples != null && samples.Count > 0)
                {
                    return profile;
                }
            }
            catch (Exception ex)
            {
                CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] EventPipe profile conversion failed: {ex}");
            }
            finally
            {
                try { File.Delete(netTraceFileToProcess); } catch {}
                if (_tempEtlxFile != null && File.Exists(_tempEtlxFile))
                {
                    try { File.Delete(_tempEtlxFile); } catch {}
                }
            }
        }

        return GenerateProfile(startTimeCopy, endTime, spansCopy);
    }

    private JsonObject ConvertNetTraceToV8Profile(string netTracePath)
    {
        _tempEtlxFile = Path.ChangeExtension(netTracePath, ".etlx");
        TraceLog.CreateFromEventPipeDataFile(netTracePath, _tempEtlxFile);

        // CPU V8 Profile variables
        var cpuNodes = new List<V8ProfileNode>();
        var cpuSamples = new List<int>();
        var cpuTimeDeltas = new List<int>();
        var cpuRootNode = new V8ProfileNode(1, "(root)", "", -1, -1);
        cpuNodes.Add(cpuRootNode);
        int cpuNextNodeId = 2;
        var cpuStackCache = new Dictionary<CallStackIndex, int>();

        // Memory V8 Profile variables
        var memNodes = new List<V8ProfileNode>();
        var memSamples = new List<int>();
        var memTimeDeltas = new List<int>();
        var memRootNode = new V8ProfileNode(1, "(root)", "", -1, -1);
        memNodes.Add(memRootNode);
        int memNextNodeId = 2;
        var memStackCache = new Dictionary<CallStackIndex, int>();

        // Aggregated memory allocations stats
        var memAllocStats = new Dictionary<string, (long bytes, int count)>();

        double startTimeMs = 0;
        double endTimeMs = 0;
        double cpuLastTimeMs = 0;
        double memStartTimeMs = 0;
        double memEndTimeMs = 0;

        using (var traceLog = new TraceLog(_tempEtlxFile))
        {
            foreach (var ev in traceLog.Events)
            {
                // Parse CPU Sample
                if (ev.ProviderName == "Microsoft-DotNETCore-SampleProfiler" && ev.EventName == "Thread/Sample")
                {
                    double timeMs = ev.TimeStampRelativeMSec;
                    if (startTimeMs == 0) startTimeMs = timeMs;
                    endTimeMs = timeMs;

                    int deltaUs = (cpuLastTimeMs == 0) ? 0 : (int)Math.Max(0, (timeMs - cpuLastTimeMs) * 1000.0);
                    cpuTimeDeltas.Add(deltaUs);
                    cpuLastTimeMs = timeMs;

                    var stack = ev.CallStack();
                    if (stack == null)
                    {
                        cpuSamples.Add(1);
                        cpuRootNode.HitCount++;
                    }
                    else
                    {
                        int leafNodeId = ResolveTraceEventStackNode(stack, cpuNodes, ref cpuNextNodeId, cpuStackCache);
                        cpuSamples.Add(leafNodeId);
                        var leafNode = cpuNodes.First(n => n.Id == leafNodeId);
                        leafNode.HitCount++;
                    }
                }
                // Parse Memory Allocation
                else if (ev is GCAllocationTickTraceData allocEv)
                {
                    double timeMs = ev.TimeStampRelativeMSec;
                    if (memStartTimeMs == 0) memStartTimeMs = timeMs;
                    memEndTimeMs = timeMs;

                    long allocAmount = allocEv.AllocationAmount;
                    string typeName = allocEv.TypeName ?? "Unknown";

                    // Aggregate memory stats by type
                    if (memAllocStats.TryGetValue(typeName, out var currentStats))
                    {
                        memAllocStats[typeName] = (currentStats.bytes + allocAmount, currentStats.count + 1);
                    }
                    else
                    {
                        memAllocStats[typeName] = (allocAmount, 1);
                    }

                    var stack = ev.CallStack();
                    if (stack == null)
                    {
                        memSamples.Add(1);
                        memRootNode.HitCount++;
                        memTimeDeltas.Add((int)allocAmount);
                    }
                    else
                    {
                        int leafNodeId = ResolveTraceEventStackNode(stack, memNodes, ref memNextNodeId, memStackCache);
                        memSamples.Add(leafNodeId);
                        var leafNode = memNodes.First(n => n.Id == leafNodeId);
                        leafNode.HitCount++;
                        memTimeDeltas.Add((int)allocAmount);
                    }
                }
            }
        }

        // Build CPU V8 JSON
        var cpuNodesArray = new JsonArray();
        foreach (var node in cpuNodes)
        {
            cpuNodesArray.Add(node.ToJson());
        }
        var cpuProfileJson = new JsonObject
        {
            ["nodes"] = cpuNodesArray,
            ["startTime"] = startTimeMs * 1000.0,
            ["endTime"] = endTimeMs * 1000.0,
            ["samples"] = new JsonArray(cpuSamples.Select(s => (JsonNode)s).ToArray()),
            ["timeDeltas"] = new JsonArray(cpuTimeDeltas.Select(d => (JsonNode)d).ToArray())
        };

        // Build Memory V8 JSON
        var memNodesArray = new JsonArray();
        foreach (var node in memNodes)
        {
            memNodesArray.Add(node.ToJson());
        }
        var memProfileJson = new JsonObject
        {
            ["nodes"] = memNodesArray,
            ["startTime"] = memStartTimeMs * 1000.0,
            ["endTime"] = memEndTimeMs * 1000.0,
            ["samples"] = new JsonArray(memSamples.Select(s => (JsonNode)s).ToArray()),
            ["timeDeltas"] = new JsonArray(memTimeDeltas.Select(d => (JsonNode)d).ToArray())
        };

        // Build Memory stats JSON array
        var memStatsArray = new JsonArray();
        foreach (var pair in memAllocStats.OrderByDescending(p => p.Value.bytes))
        {
            memStatsArray.Add(new JsonObject
            {
                ["typeName"] = pair.Key,
                ["bytes"] = pair.Value.bytes,
                ["count"] = pair.Value.count
            });
        }

        // Return wrapped dual-profile
        return new JsonObject
        {
            ["profile"] = cpuProfileJson,
            ["memoryProfile"] = memProfileJson,
            ["memoryAllocations"] = memStatsArray
        };
    }

    private int ResolveTraceEventStackNode(
        TraceCallStack stack,
        List<V8ProfileNode> nodes,
        ref int nextNodeId,
        Dictionary<CallStackIndex, int> cache)
    {
        var idx = stack.CallStackIndex;
        if (cache.TryGetValue(idx, out int existingId))
        {
            return existingId;
        }

        int parentNodeId = 1; // root
        if (stack.Caller != null)
        {
            parentNodeId = ResolveTraceEventStackNode(stack.Caller, nodes, ref nextNodeId, cache);
        }

        string funcName = "Unknown";
        string fileUrl = "";

        if (stack.CodeAddress != null)
        {
            funcName = stack.CodeAddress.FullMethodName ?? "Unknown";
            if (stack.CodeAddress.ModuleFile != null)
            {
                fileUrl = stack.CodeAddress.ModuleFile.FilePath ?? "";
            }
        }

        var parentNode = nodes.First(n => n.Id == parentNodeId);

        int existingChildId = -1;
        foreach (var childId in parentNode.Children)
        {
            var childNode = nodes.First(n => n.Id == childId);
            if (childNode.FunctionName == funcName && childNode.Url == fileUrl)
            {
                existingChildId = childId;
                break;
            }
        }

        int nodeId;
        if (existingChildId != -1)
        {
            nodeId = existingChildId;
        }
        else
        {
            nodeId = nextNodeId++;
            var newNode = new V8ProfileNode(nodeId, funcName, fileUrl, 0, 0);
            newNode.HitCount = 0;
            nodes.Add(newNode);
            parentNode.Children.Add(nodeId);
        }

        cache[idx] = nodeId;
        return nodeId;
    }

    private JsonObject GenerateProfile(DateTime start, DateTime end, List<ProfileSpan> spans)
    {
        double startTimeUs = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000.0;
        double endTimeUs = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000.0;

        if (endTimeUs <= startTimeUs)
        {
            endTimeUs = startTimeUs + 1000.0;
        }

        double durationMs = (end - start).TotalMilliseconds;
        if (durationMs < 1) durationMs = 1;

        var samples = new JsonArray();
        var timeDeltas = new JsonArray();

        int stepMs = 1;
        double currentOffsetMs = 0;

        var hitCounts = new Dictionary<int, int>();
        for (int i = 1; i <= 9; i++) hitCounts[i] = 0;

        while (currentOffsetMs < durationMs)
        {
            currentOffsetMs += stepMs;
            DateTime sampleTime = start.AddMilliseconds(currentOffsetMs);

            int activeNodeId = 3; // (idle)

            ProfileSpan? activeSpan = null;
            double minDuration = double.MaxValue;
            foreach (var span in spans)
            {
                if (sampleTime >= span.StartTime && sampleTime <= span.EndTime)
                {
                    double dur = (span.EndTime - span.StartTime).TotalMilliseconds;
                    if (dur < minDuration)
                    {
                        minDuration = dur;
                        activeSpan = span;
                    }
                }
            }

            if (activeSpan.HasValue)
            {
                var name = activeSpan.Value.Name;
                if (name == "LayoutPass")
                {
                    double spanDuration = (activeSpan.Value.EndTime - activeSpan.Value.StartTime).TotalMilliseconds;
                    double offsetInSpan = (sampleTime - activeSpan.Value.StartTime).TotalMilliseconds;
                    if (offsetInSpan < spanDuration * 0.6)
                    {
                        activeNodeId = 6; // Measure
                    }
                    else
                    {
                        activeNodeId = 7; // Arrange
                    }
                }
                else if (name == "Measure")
                {
                    activeNodeId = 6;
                }
                else if (name == "Arrange")
                {
                    activeNodeId = 7;
                }
                else if (name == "RenderFrame")
                {
                    activeNodeId = 8;
                }
                else if (name == "EvaluateConsole")
                {
                    activeNodeId = 9;
                }
            }

            samples.Add(activeNodeId);
            hitCounts[activeNodeId]++;
            timeDeltas.Add(1000); // 1ms = 1000 microseconds
        }

        var nodes = new JsonArray
        {
            new JsonObject
            {
                ["id"] = 1,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "(root)",
                    ["scriptId"] = "0",
                    ["url"] = "",
                    ["lineNumber"] = -1,
                    ["columnNumber"] = -1
                },
                ["hitCount"] = hitCounts[1],
                ["children"] = new JsonArray { 2, 3, 4 }
            },
            new JsonObject
            {
                ["id"] = 2,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "(garbage collector)",
                    ["scriptId"] = "0",
                    ["url"] = "",
                    ["lineNumber"] = -1,
                    ["columnNumber"] = -1
                },
                ["hitCount"] = hitCounts[2]
            },
            new JsonObject
            {
                ["id"] = 3,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "(idle)",
                    ["scriptId"] = "0",
                    ["url"] = "",
                    ["lineNumber"] = -1,
                    ["columnNumber"] = -1
                },
                ["hitCount"] = hitCounts[3]
            },
            new JsonObject
            {
                ["id"] = 4,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "AppLoop",
                    ["scriptId"] = "1",
                    ["url"] = "app://loop",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[4],
                ["children"] = new JsonArray { 5, 8, 9 }
            },
            new JsonObject
            {
                ["id"] = 5,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "LayoutPass",
                    ["scriptId"] = "2",
                    ["url"] = "app://layout",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[5],
                ["children"] = new JsonArray { 6, 7 }
            },
            new JsonObject
            {
                ["id"] = 6,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "Measure",
                    ["scriptId"] = "2",
                    ["url"] = "app://layout",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[6]
            },
            new JsonObject
            {
                ["id"] = 7,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "Arrange",
                    ["scriptId"] = "2",
                    ["url"] = "app://layout",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[7]
            },
            new JsonObject
            {
                ["id"] = 8,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "RenderFrame",
                    ["scriptId"] = "3",
                    ["url"] = "app://render",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[8]
            },
            new JsonObject
            {
                ["id"] = 9,
                ["callFrame"] = new JsonObject
                {
                    ["functionName"] = "EvaluateConsole",
                    ["scriptId"] = "4",
                    ["url"] = "app://console",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0
                },
                ["hitCount"] = hitCounts[9]
            }
        };

        return new JsonObject
        {
            ["nodes"] = nodes,
            ["startTime"] = startTimeUs,
            ["endTime"] = endTimeUs,
            ["samples"] = samples,
            ["timeDeltas"] = timeDeltas
        };
    }
}
