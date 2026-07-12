#nullable enable

using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class ScratchTimeMachineNodeData
{
    public string SelectedFramePayloadText { get; set; } = "";
    public bool IsPinned { get; set; }
    public int PinnedFrameIndex { get; set; }

    public ScratchTimeMachineNodeData Clone()
    {
        return new ScratchTimeMachineNodeData
        {
            SelectedFramePayloadText = this.SelectedFramePayloadText,
            IsPinned = this.IsPinned,
            PinnedFrameIndex = this.PinnedFrameIndex
        };
    }
}

public class ScratchTimeMachineNodeViewModel : ScratchNodeViewModelBase
{
    private readonly ICdpService? _cdpService;
    private string _selectedFramePayloadText = "";
    private bool _isPinned;
    private int _pinnedFrameIndex = -1;
    private bool _isDisposed;

    public ITimeMachineService? TimeMachine => _cdpService?.TimeMachine;
    public override string OutputJson => _selectedFramePayloadText;

    public override JsonNode? OutputJsonNode
    {
        get
        {
            if (TimeMachine == null) return null;
            var idx = IsPinned ? PinnedFrameIndex : TimeMachine.CurrentFrameIndex;
            var frames = TimeMachine.Frames;
            if (idx >= 0 && idx < frames.Count)
            {
                return frames[idx].Payload;
            }
            return null;
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (RaiseAndSetIfChanged(ref _isPinned, value))
            {
                if (value && _pinnedFrameIndex == -1 && TimeMachine != null)
                {
                    PinnedFrameIndex = TimeMachine.CurrentFrameIndex;
                }
                UpdatePayload();
            }
        }
    }

    public int PinnedFrameIndex
    {
        get => _pinnedFrameIndex;
        set
        {
            if (RaiseAndSetIfChanged(ref _pinnedFrameIndex, value))
            {
                if (IsPinned)
                {
                    UpdatePayload();
                }
            }
        }
    }

    public string SelectedFramePayloadText
    {
        get => _selectedFramePayloadText;
        set => RaiseAndSetIfChanged(ref _selectedFramePayloadText, value);
    }

    public int CurrentFrameIndex
    {
        get => TimeMachine?.CurrentFrameIndex ?? -1;
        set
        {
            if (TimeMachine != null && TimeMachine.CurrentFrameIndex != value)
            {
                TimeMachine.Seek(value);
            }
        }
    }

    public int MaxFrameIndex => Math.Max(0, TimeMachine?.Frames.Count ?? 1) - 1;

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepForwardCommand { get; }
    public ICommand StepBackwardCommand { get; }

    public ScratchTimeMachineNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;
        TitleBackground = Avalonia.Media.Brush.Parse("#8ab4f8");
        BorderBrush = Avalonia.Media.Brush.Parse("#1a73e8");

        AddOutputPin("frame", "Frame Data");

        PlayCommand = new RelayCommand(() => TimeMachine?.Play());
        PauseCommand = new RelayCommand(() => TimeMachine?.Pause());
        StopCommand = new RelayCommand(() =>
        {
            if (TimeMachine != null)
            {
                TimeMachine.Pause();
                TimeMachine.IsReplaying = false;
                TimeMachine.Seek(TimeMachine.Frames.Count > 0 ? TimeMachine.Frames.Count - 1 : -1);
            }
        });
        StepForwardCommand = new RelayCommand(() => TimeMachine?.StepForward());
        StepBackwardCommand = new RelayCommand(() => TimeMachine?.StepBackward());

        if (TimeMachine != null)
        {
            TimeMachine.FrameChanged += TimeMachine_FrameChanged;
            TimeMachine.PropertyChanged += TimeMachine_PropertyChanged;
            UpdatePayload();
        }
    }

    private void TimeMachine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITimeMachineService.CurrentFrameIndex))
        {
            OnPropertyChanged(nameof(CurrentFrameIndex));
        }
        else if (e.PropertyName == nameof(ITimeMachineService.Frames))
        {
            OnPropertyChanged(nameof(MaxFrameIndex));
        }
    }

    private void TimeMachine_FrameChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(MaxFrameIndex));
        UpdatePayload();
    }

    private void UpdatePayload()
    {
        if (TimeMachine == null) return;
        var idx = IsPinned ? PinnedFrameIndex : TimeMachine.CurrentFrameIndex;
        var frames = TimeMachine.Frames;
        if (idx >= 0 && idx < frames.Count)
        {
            var frame = frames[idx];
            SelectedFramePayloadText = frame.Payload?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
        }
        else
        {
            SelectedFramePayloadText = "";
        }
        OnPropertyChanged(nameof(OutputJsonNode));
    }

    public override void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (TimeMachine != null)
        {
            TimeMachine.FrameChanged -= TimeMachine_FrameChanged;
            TimeMachine.PropertyChanged -= TimeMachine_PropertyChanged;
        }
        base.Dispose();
    }
}
