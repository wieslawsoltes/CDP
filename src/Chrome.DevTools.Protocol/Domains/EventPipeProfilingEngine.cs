using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Chrome.DevTools.Protocol.Domains;

public class EventPipeProfilingEngine : IProfilingEngine
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<EventPipeProfilingEngine>();
    private readonly object _lock = new();
    private readonly List<ProfileSpan> _spans = new();
    private DateTime _startTime;
    private bool _isRunning;

    // Real-time EventPipe profiling session details
    private EventPipeSession? _session;
    private string? _tempNetTraceFile;
    private string? _tempEtlxFile;
    private Task? _copyTask;

    public string Name => "eventpipe";
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
                    new EventPipeProvider("Microsoft-Windows-DotNETRuntime", System.Diagnostics.Tracing.EventLevel.Verbose, 0x8000300201bL),
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
                        Logger.LogProfilerError("eventpipe", "Copy stream task failed", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                // Fallback to simulated profiling if EventPipe fails to launch (e.g. lacks privileges or dependencies)
                Logger.LogProfilerError("eventpipe", "Failed to initialize EventPipe profiling", ex);
                _session = null;
                _tempNetTraceFile = null;
                throw;
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
            if (!_isRunning || _startTime == DateTime.MinValue)
            {
                return new JsonObject
                {
                    ["nodes"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = 1,
                            ["callFrame"] = new JsonObject
                            {
                                ["functionName"] = "(root)",
                                ["url"] = "",
                                ["scriptId"] = "0",
                                ["lineNumber"] = -1,
                                ["columnNumber"] = -1
                            },
                            ["hitCount"] = 0
                        }
                    },
                    ["startTime"] = 0.0,
                    ["endTime"] = 0.0,
                    ["samples"] = new JsonArray(),
                    ["timeDeltas"] = new JsonArray()
                };
            }

            endTime = DateTime.UtcNow;
            _isRunning = false;
            spansCopy = new List<ProfileSpan>(_spans);
            startTimeCopy = _startTime;
            sessionToStop = _session;
            copyTaskToWait = _copyTask;
            netTraceFileToProcess = _tempNetTraceFile;

            _startTime = DateTime.MinValue;
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
                        Logger.LogProfilerInfo("eventpipe", "Warning: copy task did not finish in 15 seconds.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogProfilerError("eventpipe", "Copy task wait threw exception", ex);
                }
            }
        }

        if (netTraceFileToProcess != null && File.Exists(netTraceFileToProcess))
        {
            try
            {
                var profile = ConvertNetTraceToV8Profile(netTraceFileToProcess);
                var cpuProfile = profile["profile"] as JsonObject;
                var samples = cpuProfile?["samples"]?.AsArray();
                if (samples != null && samples.Count > 0)
                {
                    return profile;
                }
            }
            catch (Exception ex)
            {
                Logger.LogProfilerError("eventpipe", "EventPipe profile conversion failed", ex);
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

        return SimulatedProfilingEngine.GenerateProfile(startTimeCopy, endTime, spansCopy);
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

        int totalEvents = 0;
        int dotNetRuntimeEvents = 0;
        int parsedTickEvents = 0;
        int parsedSampledEvents = 0;
        int sampleProfilerEvents = 0;

        using (var traceLog = new TraceLog(_tempEtlxFile))
        {
            var clr = traceLog.Clr; // Force loading built-in CLR parser templates
            foreach (var ev in traceLog.Events)
            {
                try
                {
                    totalEvents++;
                    if (ev.ProviderName == "Microsoft-Windows-DotNETRuntime")
                    {
                        dotNetRuntimeEvents++;
                    }

                    // Parse CPU Sample
                    if (ev.ProviderName == "Microsoft-DotNETCore-SampleProfiler" && ev.EventName == "Thread/Sample")
                    {
                        sampleProfilerEvents++;
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
                        parsedTickEvents++;
                        string typeName = allocEv.TypeName ?? "Unknown";
                        ProcessAllocation(ev, typeName, allocEv.AllocationAmount, ref memStartTimeMs, ref memEndTimeMs, memAllocStats, memNodes, ref memNextNodeId, memStackCache, memSamples, memRootNode, memTimeDeltas);
                    }
                    else if ((int)ev.ID == 10 || ev.EventName == "GC/AllocationTick" || ev.EventName == "GC/AllocationTick_V2" || ev.EventName == "GC/AllocationTick_V3" || ev.EventName == "GC/AllocationTick_V4")
                    {
                        parsedTickEvents++;
                        if (TryGetDynamicAllocationTick(ev, out var typeName, out var objectSize))
                        {
                            ProcessAllocation(ev, typeName, objectSize, ref memStartTimeMs, ref memEndTimeMs, memAllocStats, memNodes, ref memNextNodeId, memStackCache, memSamples, memRootNode, memTimeDeltas);
                        }
                    }
                    else if ((int)ev.ID == 303)
                    {
                        parsedSampledEvents++;
                        try
                        {
                            byte[] data = ev.EventData();
                            if (TryParseAllocationSampled(data, ev.PointerSize, out var typeName, out var objectSize))
                            {
                                ProcessAllocation(ev, typeName, objectSize, ref memStartTimeMs, ref memEndTimeMs, memAllocStats, memNodes, ref memNextNodeId, memStackCache, memSamples, memRootNode, memTimeDeltas);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogProfilerError("eventpipe", "Failed to manually parse EventID(303)", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogProfilerError("eventpipe", $"Failed to process event {ev?.EventName} (ID {ev?.ID})", ex);
                }
            }
        }

        Logger.LogProfilerInfo("eventpipe", $"Trace Log parsing complete. Total Events: {totalEvents}, DotNetRuntime: {dotNetRuntimeEvents}, SampleProfiler: {sampleProfilerEvents}, Parsed Ticks: {parsedTickEvents}, Parsed Sampled (303): {parsedSampledEvents}");

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

        return new JsonObject
        {
            ["profile"] = cpuProfileJson,
            ["memoryProfile"] = memProfileJson,
            ["memoryAllocations"] = memStatsArray
        };
    }

    public static bool TryGetDynamicAllocationTick(TraceEvent ev, out string typeName, out long objectSize)
    {
        typeName = "Unknown";
        objectSize = 0;
        try
        {
            var typeNameObj = ev.PayloadByName("TypeName");
            var amountObj = ev.PayloadByName("AllocationAmount");
            if (typeNameObj != null)
            {
                typeName = typeNameObj.ToString() ?? "Unknown";
            }
            if (amountObj != null)
            {
                objectSize = Convert.ToInt64(amountObj);
                return true;
            }
        }
        catch
        {
            // Ignore conversion errors
        }
        return false;
    }

    public static bool TryParseAllocationSampled(byte[] data, int pointerSize, out string typeName, out long objectSize)
    {
        typeName = "Unknown";
        objectSize = 0;

        if (data == null || data.Length < 6)
        {
            return false;
        }

        int typeNameOffset = 6 + pointerSize;
        if (data.Length <= typeNameOffset)
        {
            return false;
        }

        int len = 0;
        while (typeNameOffset + len < data.Length - 1 && BitConverter.ToUInt16(data, typeNameOffset + len) != 0)
        {
            len += 2;
        }

        typeName = System.Text.Encoding.Unicode.GetString(data, typeNameOffset, len);
        int nextOffset = typeNameOffset + len + 2; // skip type name and null terminator

        if (data.Length >= nextOffset + pointerSize + 8)
        {
            nextOffset += pointerSize; // skip Address
            objectSize = BitConverter.ToInt64(data, nextOffset);
            return true;
        }

        return false;
    }

    private void ProcessAllocation(
        TraceEvent ev,
        string typeName,
        long allocAmount,
        ref double memStartTimeMs,
        ref double memEndTimeMs,
        Dictionary<string, (long bytes, int count)> memAllocStats,
        List<V8ProfileNode> memNodes,
        ref int memNextNodeId,
        Dictionary<CallStackIndex, int> memStackCache,
        List<int> memSamples,
        V8ProfileNode memRootNode,
        List<int> memTimeDeltas)
    {
        double timeMs = ev.TimeStampRelativeMSec;
        if (memStartTimeMs == 0) memStartTimeMs = timeMs;
        memEndTimeMs = timeMs;

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
}
