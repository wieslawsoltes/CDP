using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using JetBrains.Profiler.SelfApi;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol.Domains;

public class DotTraceProfilingEngine : IProfilingEngine
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<DotTraceProfilingEngine>();
    private readonly object _lock = new();
    private readonly List<ProfileSpan> _spans = new();
    private DateTime _startTime;
    private bool _isRunning;
    private string? _tempDir;

    public string Name => "dottrace";
    public bool IsRunning => _isRunning;

    public void Start()
    {
        lock (_lock)
        {
            _spans.Clear();
            _startTime = DateTime.UtcNow;
            _isRunning = true;

            _tempDir = Path.Combine(Path.GetTempPath(), $"cdp_dottrace_{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(_tempDir);
            }
            catch (Exception ex)
            {
                Logger.LogProfilerError("dottrace", "Failed to create temp dir", ex);
            }

            try
            {
                DotTrace.Init();
                var config = new DotTrace.Config();
                config.SaveToDir(_tempDir);
                DotTrace.Attach(config);
                DotTrace.StartCollectingData();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _startTime = DateTime.MinValue;
                Logger.LogProfilerError("dottrace", "Failed to start profiling", ex);
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
            DotTrace.SaveData();
        }
        catch (Exception ex)
        {
            Logger.LogProfilerError("dottrace", "SaveData failed", ex);
        }

        try
        {
            DotTrace.Detach();
        }
        catch (Exception ex)
        {
            Logger.LogProfilerError("dottrace", "Detach failed", ex);
        }

        string snapshotPath = string.Empty;
        if (!string.IsNullOrEmpty(tempDirCopy) && Directory.Exists(tempDirCopy))
        {
            try
            {
                var files = Directory.GetFiles(tempDirCopy);
                if (files.Length > 0)
                {
                    snapshotPath = files.OrderByDescending(File.GetLastWriteTime).First();
                }
                else
                {
                    snapshotPath = Path.Combine(tempDirCopy, "snapshot.dtp");
                }
            }
            catch (Exception ex)
            {
                Logger.LogProfilerError("dottrace", "Finding final .dtp file failed", ex);
                snapshotPath = Path.Combine(tempDirCopy, "snapshot.dtp");
            }
        }
        else
        {
            snapshotPath = Path.Combine(Path.GetTempPath(), "snapshot.dtp");
        }

        var simulatedProfile = SimulatedProfilingEngine.GenerateProfile(startTimeCopy, endTime, spansCopy);

        return new JsonObject
        {
            ["jetbrainsTracePath"] = snapshotPath,
            ["profile"] = simulatedProfile
        };
    }
}
