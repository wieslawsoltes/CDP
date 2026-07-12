#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol;

public interface ITimeMachineService : INotifyPropertyChanged
{
    bool IsRecording { get; set; }
    bool IsReplaying { get; set; }
    int CurrentFrameIndex { get; set; }
    IReadOnlyList<TimeMachineFrame> Frames { get; }

    event EventHandler? FrameChanged;
    event EventHandler? ReplayStateCleared;

    void StartRecording();
    void StopRecording();
    void Clear();

    void Play();
    void Pause();
    void StepForward();
    void StepBackward();
    void Seek(int index);

    void RecordEvent(string method, JsonObject? parameters);
    void RecordResponse(string method, JsonObject? parameters, JsonObject? result);
    JsonObject? GetReplayResponse(string method, JsonObject? parameters);
    JsonObject? GetReplayResponseAtFrame(string method, JsonObject? parameters, int frameIndex);

    JsonNode SaveState();
    void LoadState(JsonNode state);
}
