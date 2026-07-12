#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol;

public class TimeMachineService : ITimeMachineService, INotifyPropertyChanged
{
    private readonly List<TimeMachineFrame> _frames = new();
    private readonly object _lock = new();
    private bool _isRecording;
    private bool _isReplaying;
    private int _currentFrameIndex = -1;

    private CancellationTokenSource? _playCts;
    private int _playIntervalMs = 500;
    private readonly Dictionary<(JsonObject, JsonObject), bool> _parameterComparisonCache = new();

    public event EventHandler? FrameChanged;
    public event EventHandler? ReplayStateCleared;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                OnPropertyChanged(nameof(IsRecording));
                if (_isRecording)
                {
                    IsReplaying = false;
                }
            }
        }
    }

    public bool IsReplaying
    {
        get => _isReplaying;
        set
        {
            if (_isReplaying != value)
            {
                _isReplaying = value;
                OnPropertyChanged(nameof(IsReplaying));
                if (_isReplaying)
                {
                    IsRecording = false;
                }
            }
        }
    }

    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set
        {
            if (_currentFrameIndex != value)
            {
                _currentFrameIndex = value;
                OnPropertyChanged(nameof(CurrentFrameIndex));
            }
        }
    }

    public IReadOnlyList<TimeMachineFrame> Frames
    {
        get
        {
            lock (_lock)
            {
                return _frames.ToArray();
            }
        }
    }

    public void StartRecording()
    {
        IsRecording = true;
        lock (_lock)
        {
            _parameterComparisonCache.Clear();
        }
    }

    public void StopRecording()
    {
        IsRecording = false;
    }

    public void Clear()
    {
        Pause();
        lock (_lock)
        {
            _frames.Clear();
            _parameterComparisonCache.Clear();
        }
        CurrentFrameIndex = -1;
        ReplayStateCleared?.Invoke(this, EventArgs.Empty);
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordEvent(string method, JsonObject? parameters)
    {
        if (!IsRecording) return;

        lock (_lock)
        {
            var dotIndex = method.IndexOf('.');
            var domain = dotIndex > 0 ? method.Substring(0, dotIndex) : "General";

            var frame = new TimeMachineFrame
            {
                Index = _frames.Count,
                Timestamp = DateTime.UtcNow,
                Domain = domain,
                Type = "Event",
                Method = method,
                Params = parameters?.DeepClone() as JsonObject,
                Payload = parameters?.DeepClone() as JsonObject
            };
            _frames.Add(frame);
            CurrentFrameIndex = frame.Index;
        }
    }

    public void RecordResponse(string method, JsonObject? parameters, JsonObject? result)
    {
        if (!IsRecording) return;

        lock (_lock)
        {
            var dotIndex = method.IndexOf('.');
            var domain = dotIndex > 0 ? method.Substring(0, dotIndex) : "General";

            var frame = new TimeMachineFrame
            {
                Index = _frames.Count,
                Timestamp = DateTime.UtcNow,
                Domain = domain,
                Type = "Response",
                Method = method,
                Params = parameters?.DeepClone() as JsonObject,
                Payload = result?.DeepClone() as JsonObject
            };
            _frames.Add(frame);
            CurrentFrameIndex = frame.Index;
        }
    }

    public JsonObject? GetReplayResponse(string method, JsonObject? parameters)
    {
        return GetReplayResponseAtFrame(method, parameters, CurrentFrameIndex);
    }

    public JsonObject? GetReplayResponseAtFrame(string method, JsonObject? parameters, int frameIndex)
    {
        lock (_lock)
        {
            if (frameIndex < 0 || _frames.Count == 0)
            {
                return null;
            }
            int index = Math.Min(frameIndex, _frames.Count - 1);

            for (int i = index; i >= 0; i--)
            {
                var frame = _frames[i];
                if (frame.Type == "Response" && frame.Method == method)
                {
                    if (AreParametersMatching(parameters, frame.Params))
                    {
                        return frame.Payload?.DeepClone() as JsonObject;
                    }
                }
            }
        }
        return null;
    }

    private bool AreParametersMatching(JsonObject? a, JsonObject? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        var key = (a, b);
        if (_parameterComparisonCache.TryGetValue(key, out var match))
        {
            return match;
        }

        match = JsonNode.DeepEquals(a, b);
        _parameterComparisonCache[key] = match;
        return match;
    }

    public void Seek(int index)
    {
        Pause();
        lock (_lock)
        {
            if (_frames.Count == 0)
            {
                CurrentFrameIndex = -1;
                return;
            }
            if (index < 0) index = 0;
            if (index >= _frames.Count) index = _frames.Count - 1;
            
            IsReplaying = true;
            CurrentFrameIndex = index;
        }

        ReplayStateCleared?.Invoke(this, EventArgs.Empty);
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StepForward()
    {
        Pause();
        int nextIndex = CurrentFrameIndex + 1;
        lock (_lock)
        {
            if (nextIndex >= _frames.Count) return;
        }
        Seek(nextIndex);
    }

    public void StepBackward()
    {
        Pause();
        int prevIndex = CurrentFrameIndex - 1;
        if (prevIndex < 0) return;
        Seek(prevIndex);
    }

    public void Play()
    {
        if (_playCts != null) return; // Already playing

        IsReplaying = true;
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;

        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_playIntervalMs));
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int totalFrames;
                    lock (_lock)
                    {
                        totalFrames = _frames.Count;
                    }

                    if (CurrentFrameIndex >= totalFrames - 1)
                    {
                        // Stop at the end
                        break;
                    }

                    // Move to next frame
                    int nextIndex = CurrentFrameIndex + 1;
                    
                    // We must seek on UI thread or standard invocation, but since Seek raises events,
                    // we update CurrentFrameIndex and trigger events.
                    CurrentFrameIndex = nextIndex;
                    ReplayStateCleared?.Invoke(this, EventArgs.Empty);
                    FrameChanged?.Invoke(this, EventArgs.Empty);

                    await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Playback stopped
            }
            finally
            {
                _playCts = null;
                OnPropertyChanged(nameof(Frames)); // refresh play states
            }
        }, token);
    }

    public void Pause()
    {
        if (_playCts != null)
        {
            _playCts.Cancel();
            _playCts = null;
        }
    }

    public JsonNode SaveState()
    {
        var array = new JsonArray();
        lock (_lock)
        {
            foreach (var frame in _frames)
            {
                var item = new JsonObject
                {
                    ["index"] = frame.Index,
                    ["timestamp"] = frame.Timestamp.ToString("o"),
                    ["domain"] = frame.Domain,
                    ["type"] = frame.Type,
                    ["method"] = frame.Method,
                    ["params"] = frame.Params?.DeepClone(),
                    ["payload"] = frame.Payload?.DeepClone()
                };
                array.Add(item);
            }
        }

        var root = new JsonObject
        {
            ["frames"] = array,
            ["currentIndex"] = CurrentFrameIndex
        };
        return root;
    }

    public void LoadState(JsonNode state)
    {
        Pause();
        if (state is not JsonObject obj) return;

        lock (_lock)
        {
            _frames.Clear();
            _parameterComparisonCache.Clear();
            var array = obj["frames"] as JsonArray;
            if (array != null)
            {
                foreach (var node in array)
                {
                    if (node is not JsonObject item) continue;

                    var frame = new TimeMachineFrame
                    {
                        Index = (int)(item["index"] ?? 0),
                        Timestamp = DateTime.Parse((string?)item["timestamp"] ?? DateTime.UtcNow.ToString()),
                        Domain = (string?)item["domain"] ?? "",
                        Type = (string?)item["type"] ?? "",
                        Method = (string?)item["method"] ?? "",
                        Params = item["params"]?.DeepClone() as JsonObject,
                        Payload = item["payload"]?.DeepClone() as JsonObject
                    };
                    _frames.Add(frame);
                }
            }

            CurrentFrameIndex = (int?)(obj["currentIndex"]) ?? (_frames.Count > 0 ? _frames.Count - 1 : -1);
            if (_frames.Count > 0)
            {
                IsReplaying = true;
            }
        }

        ReplayStateCleared?.Invoke(this, EventArgs.Empty);
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
