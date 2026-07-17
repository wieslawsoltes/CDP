using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using JetBrains.Profiler.Api;
using JetBrains.Profiler.SelfApi;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol.Domains;

public class DotMemoryProfilingEngine : IProfilingEngine
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<DotMemoryProfilingEngine>();
    private readonly object _lock = new();
    private readonly List<ProfileSpan> _spans = new();
    private DateTime _startTime;
    private bool _isRunning;
    private string? _tempDir;

    public string Name => "dotmemory";
    public bool IsRunning => _isRunning;
    public string? TempDir => _tempDir;

    public void Start()
    {
        lock (_lock)
        {
            _spans.Clear();
            _startTime = DateTime.UtcNow;
            _isRunning = true;

            _tempDir = Path.Combine(Path.GetTempPath(), $"cdp_dotmemory_{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(_tempDir);
            }
            catch (Exception ex)
            {
                Logger.LogProfilerError("dotmemory", "Failed to create temp dir", ex);
            }

            try
            {
                DotMemory.Init();
                var config = new DotMemory.Config();
                config.SaveToDir(_tempDir);
                DotMemory.Attach(config);
                MemoryProfiler.CollectAllocations(true);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _startTime = DateTime.MinValue;
                Logger.LogProfilerError("dotmemory", "Failed to start profiling", ex);
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

    public string TakeSnapshot(string name)
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                Logger.LogProfilerInfo("dotmemory", "Cannot take snapshot, engine is not running.");
                return string.Empty;
            }
        }

        try
        {
            MemoryProfiler.GetSnapshot(name);

            // Give a tiny window for the file to be written, if needed, but usually it blocks until written.
            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                var files = Directory.GetFiles(_tempDir, "*.dmw");
                if (files.Length > 0)
                {
                    return files.OrderByDescending(File.GetLastWriteTime).First();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogProfilerError("dotmemory", "GetSnapshot failed", ex);
        }

        return string.Empty;
    }

    public JsonObject Stop()
    {
        DateTime endTime;
        List<ProfileSpan> spansCopy;
        DateTime startTimeCopy;
        string? tempDirCopy;

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
            tempDirCopy = _tempDir;
            _startTime = DateTime.MinValue;
            _tempDir = null;
        }

        try
        {
            MemoryProfiler.GetSnapshot("CDP_Snapshot_Stop");
        }
        catch (Exception ex)
        {
            Logger.LogProfilerError("dotmemory", "GetSnapshot on Stop failed", ex);
        }

        try
        {
            DotMemory.Detach();
        }
        catch (Exception ex)
        {
            Logger.LogProfilerError("dotmemory", "Detach failed", ex);
        }

        string snapshotPath = string.Empty;
        if (!string.IsNullOrEmpty(tempDirCopy) && Directory.Exists(tempDirCopy))
        {
            try
            {
                var files = Directory.GetFiles(tempDirCopy, "*.dmw");
                if (files.Length > 0)
                {
                    snapshotPath = files.OrderByDescending(File.GetLastWriteTime).First();
                }
                else
                {
                    snapshotPath = Path.Combine(tempDirCopy, "snapshot.dmw");
                }
            }
            catch (Exception ex)
            {
                Logger.LogProfilerError("dotmemory", "Finding final .dmw file failed", ex);
                snapshotPath = Path.Combine(tempDirCopy, "snapshot.dmw");
            }
        }
        else
        {
            snapshotPath = Path.Combine(Path.GetTempPath(), "snapshot.dmw");
        }

        var simulatedProfile = SimulatedProfilingEngine.GenerateProfile(startTimeCopy, endTime, spansCopy);
        var simulatedMemoryProfile = SimulatedProfilingEngine.GenerateProfile(startTimeCopy, endTime, spansCopy);

        return new JsonObject
        {
            ["jetbrainsMemorySnapshotPath"] = snapshotPath,
            ["profile"] = simulatedProfile,
            ["memoryProfile"] = simulatedMemoryProfile
        };
    }
}
