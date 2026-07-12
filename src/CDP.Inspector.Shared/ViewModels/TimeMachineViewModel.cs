#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class TimeMachineViewModel : ViewModelBase, IStateProvider
{
    private readonly ICdpService _cdpService;
    private TimeMachineFrame? _selectedFrame;
    private string _selectedFramePayloadText = "";
    private ScratchNodeViewModelBase? _selectedFrameViewModel;
    private string _searchText = "";
    private string _selectedSortOption = "Index Ascending";
    private string _selectedDomainFilter = "All";
    private IReadOnlyList<TimeMachineFrame> _filteredFrames = Array.Empty<TimeMachineFrame>();
    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;

    public ITimeMachineService TimeMachine => _cdpService.TimeMachine;
    public DiffViewModel Diff { get; } = new DiffViewModel();

    public bool IsRecording
    {
        get => TimeMachine.IsRecording;
        set => TimeMachine.IsRecording = value;
    }

    public string RecordingIndicatorColor => IsRecording ? "#ff5252" : "#808080";

    public bool IsReplaying
    {
        get => TimeMachine.IsReplaying;
        set => TimeMachine.IsReplaying = value;
    }

    public int CurrentFrameIndex
    {
        get => TimeMachine.CurrentFrameIndex;
        set
        {
            if (TimeMachine.CurrentFrameIndex != value)
            {
                TimeMachine.Seek(value);
            }
        }
    }

    public int MaxFrameIndex => Math.Max(0, TimeMachine.Frames.Count - 1);

    public TimeMachineFrame? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFrame, value))
            {
                UpdateSelectedFrameDetails();
            }
        }
    }

    public string SelectedFramePayloadText
    {
        get => _selectedFramePayloadText;
        private set => RaiseAndSetIfChanged(ref _selectedFramePayloadText, value);
    }

    public ScratchNodeViewModelBase? SelectedFrameViewModel
    {
        get => _selectedFrameViewModel;
        private set => RaiseAndSetIfChanged(ref _selectedFrameViewModel, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchText, value))
            {
                UpdateFilteredFrames();
            }
        }
    }

    public IReadOnlyList<TimeMachineFrame> FilteredFrames
    {
        get => _filteredFrames;
        private set => RaiseAndSetIfChanged(ref _filteredFrames, value);
    }

    public IReadOnlyList<string> SortOptions { get; } = new[]
    {
        "Index Ascending",
        "Index Descending",
        "Timestamp",
        "Method",
        "Domain"
    };

    public IReadOnlyList<string> DomainFilters { get; } = new[]
    {
        "All",
        "DOM",
        "Accessibility",
        "Console",
        "Network",
        "Performance",
        "MVVM",
        "Application"
    };

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedSortOption, value))
            {
                UpdateFilteredFrames();
            }
        }
    }

    public string SelectedDomainFilter
    {
        get => _selectedDomainFilter;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedDomainFilter, value))
            {
                UpdateFilteredFrames();
            }
        }
    }

    public SplitNode? LayoutRoot { get => _layoutRoot; set => RaiseAndSetIfChanged(ref _layoutRoot, value); }
    public BoxNode? SelectedPane { get => _selectedPane; set => RaiseAndSetIfChanged(ref _selectedPane, value); }

    // Commands
    public ICommand ToggleRecordCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepForwardCommand { get; }
    public ICommand StepBackwardCommand { get; }
    public ICommand ClearCommand { get; }

    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    // File pickers bound from View code-behind
    public Func<string, string?, Task<string?>>? FileSavePickerHandler { get; set; }
    public Func<string?, Task<string?>>? FileLoadPickerHandler { get; set; }

    public TimeMachineViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));

        var frames = new BoxNode("TimeMachineFrames", "Recorded Frames", "HistoryIcon") { BackgroundTint = "#292a2d" };
        var details = new BoxNode("TimeMachineDetails", "Frame Details & Diffs", "DeveloperBoardIcon") { BackgroundTint = "#292a2d" };
        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, frames, details) { SplitterRatio = 0.4 };
        SelectedPane = frames;
        TimeMachine.PropertyChanged += TimeMachine_PropertyChanged;
        TimeMachine.FrameChanged += TimeMachine_FrameChanged;

        ToggleRecordCommand = new RelayCommand(() => IsRecording = !IsRecording);
        PlayCommand = new RelayCommand(TimeMachine.Play);
        PauseCommand = new RelayCommand(TimeMachine.Pause);
        StopCommand = new RelayCommand(() =>
        {
            TimeMachine.Pause();
            IsReplaying = false;
            TimeMachine.Seek(TimeMachine.Frames.Count > 0 ? TimeMachine.Frames.Count - 1 : -1);
        });
        StepForwardCommand = new RelayCommand(TimeMachine.StepForward);
        StepBackwardCommand = new RelayCommand(TimeMachine.StepBackward);
        ClearCommand = new RelayCommand(TimeMachine.Clear);

        ExportCommand = new RelayCommand(async () => await ExportSessionAsync());
        ImportCommand = new RelayCommand(async () => await ImportSessionAsync());

        UpdateFilteredFrames();
    }

    private void TimeMachine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITimeMachineService.IsRecording))
        {
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(RecordingIndicatorColor));
        }
        else if (e.PropertyName == nameof(ITimeMachineService.IsReplaying))
        {
            OnPropertyChanged(nameof(IsReplaying));
        }
        else if (e.PropertyName == nameof(ITimeMachineService.CurrentFrameIndex))
        {
            OnPropertyChanged(nameof(CurrentFrameIndex));
        }
        else if (e.PropertyName == nameof(ITimeMachineService.Frames))
        {
            OnPropertyChanged(nameof(MaxFrameIndex));
            UpdateFilteredFrames();
        }
    }

    private void TimeMachine_FrameChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(MaxFrameIndex));
        UpdateFilteredFrames();

        // Select the active frame automatically on scrubber changes
        var frames = TimeMachine.Frames;
        var activeIndex = TimeMachine.CurrentFrameIndex;
        if (activeIndex >= 0 && activeIndex < frames.Count)
        {
            SelectedFrame = frames[activeIndex];
        }
    }

    private void UpdateFilteredFrames()
    {
        var frames = TimeMachine.Frames;
        IEnumerable<TimeMachineFrame> query = frames;

        // Apply SearchText filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(f => f.Domain.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     f.Method.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply Domain Filter
        if (!string.IsNullOrWhiteSpace(SelectedDomainFilter) && !string.Equals(SelectedDomainFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(f => string.Equals(f.Domain, SelectedDomainFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply Sorting
        query = SelectedSortOption switch
        {
            "Index Descending" => query.OrderByDescending(f => f.Index),
            "Timestamp" => query.OrderBy(f => f.Timestamp),
            "Method" => query.OrderBy(f => f.Method),
            "Domain" => query.OrderBy(f => f.Domain),
            _ => query.OrderBy(f => f.Index) // Default to "Index Ascending"
        };

        FilteredFrames = query.ToList();
    }

    private void UpdateSelectedFrameDetails()
    {
        if (_selectedFrameViewModel != null)
        {
            _selectedFrameViewModel.Dispose();
            SelectedFrameViewModel = null;
        }

        if (SelectedFrame == null)
        {
            SelectedFramePayloadText = "";
            Diff.SetCompareTexts("Original", "", "Modified", "");
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var payloadJson = SelectedFrame.Payload?.ToJsonString(options) ?? "{}";
        SelectedFramePayloadText = payloadJson;

        TimeMachineFrame? prevFrame = null;
        var frames = TimeMachine.Frames;
        var idx = SelectedFrame.Index;
        if (idx > 0 && idx - 1 < frames.Count)
        {
            prevFrame = frames[idx - 1];
        }

        var prevJson = prevFrame?.Payload?.ToJsonString(options) ?? "{}";

        var leftTitle = prevFrame != null ? $"Frame #{prevFrame.Index} ({prevFrame.Method})" : "No Previous Frame";
        var rightTitle = $"Frame #{SelectedFrame.Index} ({SelectedFrame.Method})";

        Diff.SetCompareTexts(leftTitle, prevJson, rightTitle, payloadJson);

        // Instantiate transient ViewModel matching the domain type
        ScratchNodeViewModelBase? transientVm = null;
        switch (SelectedFrame.Domain)
        {
            case "DOM":
                transientVm = new ScratchDomNodeViewModel(_cdpService);
                break;
            case "Console":
                transientVm = new ScratchConsoleNodeViewModel(_cdpService);
                break;
            case "Network":
                transientVm = new ScratchNetworkNodeViewModel(_cdpService);
                break;
            case "Page":
                {
                    var pageVm = new ScratchPageNodeViewModel(_cdpService);
                    string? base64 = null;
                    if (SelectedFrame.Payload != null && SelectedFrame.Payload.TryGetPropertyValue("data", out var dataVal))
                    {
                        base64 = dataVal?.GetValue<string>();
                    }
                    else if (SelectedFrame.Params != null && SelectedFrame.Params.TryGetPropertyValue("data", out var paramVal))
                    {
                        base64 = paramVal?.GetValue<string>();
                    }
                    if (!string.IsNullOrEmpty(base64))
                    {
                        pageVm.ScreenshotBase64 = base64;
                    }
                    transientVm = pageVm;
                }
                break;
            case "Accessibility":
                transientVm = new ScratchAccessibilityNodeViewModel(_cdpService);
                break;
            case "Performance":
                transientVm = new ScratchPerformanceNodeViewModel(_cdpService);
                break;
            case "Application":
                transientVm = new ScratchApplicationNodeViewModel(_cdpService);
                break;
            case "Mvvm":
            case "MVVM":
                transientVm = new ScratchMvvmNodeViewModel(_cdpService);
                break;
        }

        if (transientVm != null)
        {
            if (transientVm is IImportExportNode ieNode)
            {
                ieNode.RawJsonData = payloadJson;
            }
            SelectedFrameViewModel = transientVm;
        }
    }

    private async Task ExportSessionAsync()
    {
        if (FileSavePickerHandler == null) return;
        var path = await FileSavePickerHandler("Save Time Machine Session", "session.tm");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var node = SaveState();
            var json = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (json != null)
            {
                await File.WriteAllTextAsync(path, json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to export session: {ex.Message}");
        }
    }

    private async Task ImportSessionAsync()
    {
        if (FileLoadPickerHandler == null) return;
        var path = await FileLoadPickerHandler("Load Time Machine Session");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var node = JsonNode.Parse(json);
            if (node != null)
            {
                LoadState(node);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to import session: {ex.Message}");
        }
    }

    #region IStateProvider Implementation

    public string StateKey => "timemachine";

    public JsonNode? SaveState()
    {
        return TimeMachine.SaveState();
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode != null)
        {
            TimeMachine.LoadState(stateNode);
        }
    }

    #endregion
}
