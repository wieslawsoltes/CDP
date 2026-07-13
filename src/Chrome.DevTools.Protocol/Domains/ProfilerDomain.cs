using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

            case "setProfilingEngine":
                {
                    var engineName = @params["engineName"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(engineName))
                    {
                        throw new ArgumentException("Parameter 'engineName' is required.");
                    }
                    var state = _states.GetOrAdd(session, s => new ProfilerState());
                    state.SetEngine(engineName);
                    return Task.FromResult(new JsonObject());
                }

            case "getProfilingEngine":
                {
                    var state = _states.GetOrAdd(session, s => new ProfilerState());
                    return Task.FromResult(new JsonObject
                    {
                        ["engineName"] = state.GetEngineName()
                    });
                }

            case "takeJetBrainsMemorySnapshot":
                {
                    var name = @params["name"]?.GetValue<string>() ?? "Snapshot";
                    var state = _states.GetOrAdd(session, s => new ProfilerState());
                    string snapshotPath = "";
                    if (state.ActiveEngine is DotMemoryProfilingEngine dotMemoryEngine)
                    {
                        snapshotPath = dotMemoryEngine.TakeSnapshot(name);
                    }
                    else
                    {
                        try
                        {
                            JetBrains.Profiler.Api.MemoryProfiler.GetSnapshot(name);
                            var tempDir = Path.GetTempPath();
                            var files = Directory.GetFiles(tempDir, "*.dmw");
                            if (files.Length > 0)
                            {
                                snapshotPath = files.OrderByDescending(File.GetLastWriteTime).First();
                            }
                            else
                            {
                                snapshotPath = Path.Combine(tempDir, $"{name}_fallback.dmw");
                            }
                        }
                        catch (Exception ex)
                        {
                            CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] takeJetBrainsMemorySnapshot fallback failed: {ex.Message}");
                            snapshotPath = Path.Combine(Path.GetTempPath(), $"{name}_fallback.dmw");
                        }
                    }

                    return Task.FromResult(new JsonObject
                    {
                        ["snapshotPath"] = snapshotPath
                    });
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
    private IProfilingEngine _activeEngine;

    public IProfilingEngine ActiveEngine => _activeEngine;

    public bool IsRunning => _activeEngine.IsRunning;

    public ProfilerState()
    {
        _activeEngine = new EventPipeProfilingEngine();
    }

    public void SetEngine(string name)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Cannot change profiling engine while profiling is active/running.");
        }
        if (name.Equals("eventpipe", StringComparison.OrdinalIgnoreCase))
        {
            _activeEngine = new EventPipeProfilingEngine();
        }
        else if (name.Equals("simulated", StringComparison.OrdinalIgnoreCase))
        {
            _activeEngine = new SimulatedProfilingEngine();
        }
        else if (name.Equals("dottrace", StringComparison.OrdinalIgnoreCase))
        {
            _activeEngine = new DotTraceProfilingEngine();
        }
        else if (name.Equals("dotmemory", StringComparison.OrdinalIgnoreCase))
        {
            _activeEngine = new DotMemoryProfilingEngine();
        }
        else
        {
            throw new ArgumentException($"Unknown profiling engine: {name}");
        }
    }

    public string GetEngineName()
    {
        return _activeEngine.Name;
    }

    public void Start()
    {
        try
        {
            _activeEngine.Start();
        }
        catch (Exception ex)
        {
            CdpServer.OriginalOut.WriteLine($"[CDP PROFILER] Engine {_activeEngine.Name} failed to start: {ex.Message}");
            if (_activeEngine is EventPipeProfilingEngine)
            {
                CdpServer.OriginalOut.WriteLine("[CDP PROFILER] Falling back to simulated profiling engine.");
                _activeEngine = new SimulatedProfilingEngine();
                _activeEngine.Start();
            }
            else
            {
                throw;
            }
        }
    }

    public JsonObject Stop()
    {
        return _activeEngine.Stop();
    }

    public void AddSpan(ProfileSpan span)
    {
        _activeEngine.AddSpan(span);
    }

    public static bool TryParseAllocationSampled(byte[] data, int pointerSize, out string typeName, out long objectSize)
    {
        return EventPipeProfilingEngine.TryParseAllocationSampled(data, pointerSize, out typeName, out objectSize);
    }
}
