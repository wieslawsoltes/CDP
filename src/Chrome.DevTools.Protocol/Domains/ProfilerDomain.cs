using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public bool IsRunning => _isRunning;

    public void Start()
    {
        lock (_lock)
        {
            _spans.Clear();
            _startTime = DateTime.UtcNow;
            _isRunning = true;
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

        lock (_lock)
        {
            endTime = DateTime.UtcNow;
            _isRunning = false;
            spansCopy = new List<ProfileSpan>(_spans);
            startTimeCopy = _startTime;
        }

        return GenerateProfile(startTimeCopy, endTime, spansCopy);
    }

    private JsonObject GenerateProfile(DateTime start, DateTime end, List<ProfileSpan> spans)
    {
        double startTimeSec = ((DateTimeOffset)start).ToUnixTimeMilliseconds() / 1000.0;
        double endTimeSec = ((DateTimeOffset)end).ToUnixTimeMilliseconds() / 1000.0;

        if (endTimeSec <= startTimeSec)
        {
            endTimeSec = startTimeSec + 0.001;
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
            ["startTime"] = startTimeSec,
            ["endTime"] = endTimeSec,
            ["samples"] = samples,
            ["timeDeltas"] = timeDeltas
        };
    }
}
