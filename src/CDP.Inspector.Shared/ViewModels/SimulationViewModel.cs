using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class SimulationViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<SimulationViewModel>();
    private readonly ICdpService _cdpService;
    private readonly Func<DomNodeModel?> _getSelectedNodeFunc;
    private readonly Func<bool> _isHighlightActiveFunc;
    private readonly Func<int, (string? Role, string? Name)> _getAxDetailsFunc;
    private readonly Func<bool> _isInspectModeActiveFunc;
    private readonly Func<int, DomNodeModel?> _getDomNodeFunc;
    private readonly Func<bool> _useAutomationSelectorsFunc;
    private string _lastClickedSelector = "";

    public event EventHandler<InteractionEventArgs>? InteractionDispatched;

    private JsonObject? _highlightBoxModel;
    private string? _highlightElementType;
    private string? _highlightAxRole;
    private string? _highlightAxName;
    private bool _isHighlightOverlayVisible;
    private ReplayIndicatorInfo? _activeReplayIndicator;

    private bool _isInspectQueryInFlight;
    private double _lastInspectX;
    private double _lastInspectY;
    private bool _hasPendingInspect;
    private double _pendingInspectX;
    private double _pendingInspectY;
    private int _hoverGeneration;
    
    private string _inputSimText = "";
    private string _selectedKey = "Enter";
    private string _scrollDeltaYText = "100";
    private bool _isAdvancedToolsExpanded = false;
    private bool _isAdvancedToolsPinned = true;

    private double _advancedToolsPanelHeight = 220.0;

    public bool IsAdvancedToolsExpanded
    {
        get => _isAdvancedToolsExpanded;
        set => RaiseAndSetIfChanged(ref _isAdvancedToolsExpanded, value);
    }

    public bool IsAdvancedToolsPinned
    {
        get => _isAdvancedToolsPinned;
        set => RaiseAndSetIfChanged(ref _isAdvancedToolsPinned, value);
    }

    public double AdvancedToolsPanelHeight
    {
        get => _advancedToolsPanelHeight;
        set => RaiseAndSetIfChanged(ref _advancedToolsPanelHeight, value);
    }

    private bool _isSimulationContextMenuEnabled = true;

    public bool IsSimulationContextMenuEnabled
    {
        get => _isSimulationContextMenuEnabled;
        set => RaiseAndSetIfChanged(ref _isSimulationContextMenuEnabled, value);
    }
    
    private string _widthText = "800";
    private string _heightText = "600";
    private string _scaleFactorText = "1.0";
    private bool _isMobileActive;
    private Bitmap? _screenshotImage;
    private WriteableBitmap? _tiledBitmap1;
    private WriteableBitmap? _tiledBitmap2;
    private bool _useBitmap1 = true;
    private SkiaSharp.SKColorType _lastColorType = SkiaSharp.SKColorType.Unknown;
    private double _deviceWidth = 800;
    private double _deviceHeight = 600;

    // Zoom state
    private double _zoomLevel = 1.0;
    private bool _isFitZoomActive = true;
    private string _selectedZoomPreset = "Fit";
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _zoomPresets = new()
    {
        "Fit", "25%", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%"
    };

    public System.Collections.ObjectModel.ObservableCollection<string> ZoomPresets => _zoomPresets;

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            double clamped = Math.Clamp(value, 0.1, 5.0);
            if (RaiseAndSetIfChanged(ref _zoomLevel, clamped))
            {
                OnPropertyChanged(nameof(ZoomLevelText));
            }
        }
    }

    public bool IsFitZoomActive
    {
        get => _isFitZoomActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isFitZoomActive, value))
            {
                if (value)
                {
                    _selectedZoomPreset = "Fit";
                    OnPropertyChanged(nameof(SelectedZoomPreset));
                }
                OnPropertyChanged(nameof(ZoomLevelText));
            }
        }
    }

    public string SelectedZoomPreset
    {
        get => _selectedZoomPreset;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedZoomPreset, value))
            {
                ApplyZoomPreset(value);
            }
        }
    }

    public string ZoomLevelText => IsFitZoomActive ? "Fit" : $"{ZoomLevel * 100:F0}%";

    // Pan state
    private double _panX = 0.0;
    private double _panY = 0.0;
    private bool _isPanModeActive = false;

    public double PanX
    {
        get => _panX;
        set => RaiseAndSetIfChanged(ref _panX, value);
    }

    public double PanY
    {
        get => _panY;
        set => RaiseAndSetIfChanged(ref _panY, value);
    }

    public bool IsPanModeActive
    {
        get => _isPanModeActive;
        set => RaiseAndSetIfChanged(ref _isPanModeActive, value);
    }

    private System.Collections.ObjectModel.ObservableCollection<TargetViewItemViewModel> _targetViews = new()
    {
        new TargetViewItemViewModel("all", "All Windows & Popups"),
        new TargetViewItemViewModel("main", "Main Window Only")
    };
    private TargetViewItemViewModel? _selectedTargetView;

    public System.Collections.ObjectModel.ObservableCollection<TargetViewItemViewModel> TargetViews
    {
        get => _targetViews;
        set => RaiseAndSetIfChanged(ref _targetViews, value);
    }

    public TargetViewItemViewModel? SelectedTargetView
    {
        get => _selectedTargetView;
        set => RaiseAndSetIfChanged(ref _selectedTargetView, value);
    }

    public void RefreshTargetViews()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var existingSelectedId = SelectedTargetView?.Id ?? "all";

                var newViews = new List<TargetViewItemViewModel>
                {
                    new TargetViewItemViewModel("all", "All Visible Windows & Popups"),
                    new TargetViewItemViewModel("main", "Main Window Only")
                };

                int windowIndex = 2;
                foreach (var target in Avalonia.Diagnostics.Cdp.CdpServer.GetWindows())
                {
                    if (target.Window != null && target.Window.IsVisible)
                    {
                        string title = !string.IsNullOrWhiteSpace(target.Title) ? target.Title : $"Window #{windowIndex++}";
                        string id = target.Id;
                        if (!newViews.Exists(x => x.Id == id))
                        {
                            newViews.Add(new TargetViewItemViewModel(id, $"Window: {title}"));
                        }
                    }
                }

                var openPopups = new List<Avalonia.Controls.Primitives.Popup>();
                var visited = new HashSet<Avalonia.Visual>();
                foreach (var target in Avalonia.Diagnostics.Cdp.CdpServer.GetWindows())
                {
                    if (target.Window != null)
                    {
                        Avalonia.Diagnostics.Cdp.CdpVisualTreeHelper.FindOpenPopups(target.Window, openPopups, visited);
                    }
                }

                int popupIndex = 1;
                foreach (var popup in openPopups)
                {
                    if (popup.IsOpen)
                    {
                        string popupType = popup.Parent?.GetType().Name ?? "Popup";
                        string id = $"popup_{popup.GetHashCode()}";
                        newViews.Add(new TargetViewItemViewModel(id, $"Popup: {popupType} #{popupIndex++}"));
                    }
                }

                bool matches = TargetViews.Count == newViews.Count;
                if (matches)
                {
                    for (int i = 0; i < TargetViews.Count; i++)
                    {
                        if (TargetViews[i].Id != newViews[i].Id || TargetViews[i].Title != newViews[i].Title)
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (!matches)
                {
                    TargetViews.Clear();
                    foreach (var item in newViews)
                    {
                        TargetViews.Add(item);
                    }

                    SelectedTargetView = System.Linq.Enumerable.FirstOrDefault(TargetViews, x => x.Id == existingSelectedId)
                                      ?? System.Linq.Enumerable.FirstOrDefault(TargetViews);
                }
            }
            catch { }
        });
    }

    // Virtual Window Frame state & Metadata
    private bool _isWindowFrameVisible = true;
    private string _windowTitle = "Target Application";
    private string _windowStateText = "Normal";

    public bool IsWindowFrameVisible
    {
        get => _isWindowFrameVisible;
        set => RaiseAndSetIfChanged(ref _isWindowFrameVisible, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    public string WindowStateText
    {
        get => _windowStateText;
        set => RaiseAndSetIfChanged(ref _windowStateText, value);
    }

    private readonly System.Collections.ObjectModel.ObservableCollection<DevicePreset> _devicePresets = new()
    {
        new DevicePreset("Responsive (Reset)", 0, 0, 1.0, false),
        new DevicePreset("Desktop (720p)", 1280, 720, 1.0, false),
        new DevicePreset("Desktop (1080p)", 1920, 1080, 1.0, false),
        new DevicePreset("Desktop (1440p 2K)", 2560, 1440, 1.0, false),
        new DevicePreset("Desktop (4K)", 3840, 2160, 1.0, false),
        new DevicePreset("iPad Air", 820, 1180, 2.0, true),
        new DevicePreset("iPhone 14 Pro", 393, 852, 3.0, true),
        new DevicePreset("iPhone SE", 375, 667, 2.0, true),
        new DevicePreset("Pixel 7", 412, 915, 2.625, true)
    };
    private DevicePreset? _selectedDevicePreset;

    public System.Collections.ObjectModel.ObservableCollection<DevicePreset> DevicePresets => _devicePresets;

    public DevicePreset? SelectedDevicePreset
    {
        get => _selectedDevicePreset;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedDevicePreset, value))
            {
                OnDevicePresetChanged();
            }
        }
    }

    // Page control
    private string _navigateUrlText = "http://localhost:9222/about";

    // Modifiers
    private bool _isCtrlActive;
    private bool _isShiftActive;
    private bool _isAltActive;
    private bool _isMetaActive;

    // Advanced mouse
    private string _mousePointXText = "0";
    private string _mousePointYText = "0";
    private string _mouseDragEndXText = "100";
    private string _mouseDragEndYText = "100";
    private string _mouseButtonText = "left";
    private string _mouseClickCountText = "1";

    public string InputSimText
    {
        get => _inputSimText;
        set
        {
            if (RaiseAndSetIfChanged(ref _inputSimText, value))
            {
                ((RelayCommand)SendTextCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedKey, value))
            {
                ((RelayCommand)SendKeyCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ScrollDeltaYText
    {
        get => _scrollDeltaYText;
        set => RaiseAndSetIfChanged(ref _scrollDeltaYText, value);
    }

    public string WidthText
    {
        get => _widthText;
        set => RaiseAndSetIfChanged(ref _widthText, value);
    }

    public string HeightText
    {
        get => _heightText;
        set => RaiseAndSetIfChanged(ref _heightText, value);
    }

    public string ScaleFactorText
    {
        get => _scaleFactorText;
        set => RaiseAndSetIfChanged(ref _scaleFactorText, value);
    }

    public bool IsMobileActive
    {
        get => _isMobileActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isMobileActive, value))
            {
                _ = UpdateTouchEmulationAsync(value);
            }
        }
    }

    private async Task UpdateTouchEmulationAsync(bool enabled)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Emulation.setTouchEmulationEnabled", new JsonObject
            {
                ["enabled"] = enabled
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Failed to set touch emulation", ex);
        }
    }

    public Bitmap? ScreenshotImage
    {
        get => _screenshotImage;
        private set => RaiseAndSetIfChanged(ref _screenshotImage, value);
    }

    public double DeviceWidth
    {
        get => _deviceWidth;
        private set => RaiseAndSetIfChanged(ref _deviceWidth, value);
    }

    public double DeviceHeight
    {
        get => _deviceHeight;
        private set => RaiseAndSetIfChanged(ref _deviceHeight, value);
    }

    public JsonObject? HighlightBoxModel
    {
        get => _highlightBoxModel;
        set => RaiseAndSetIfChanged(ref _highlightBoxModel, value);
    }

    public string? HighlightElementType
    {
        get => _highlightElementType;
        set => RaiseAndSetIfChanged(ref _highlightElementType, value);
    }

    public string? HighlightAxRole
    {
        get => _highlightAxRole;
        set => RaiseAndSetIfChanged(ref _highlightAxRole, value);
    }

    public string? HighlightAxName
    {
        get => _highlightAxName;
        set => RaiseAndSetIfChanged(ref _highlightAxName, value);
    }

    public bool IsHighlightOverlayVisible
    {
        get => _isHighlightOverlayVisible;
        set => RaiseAndSetIfChanged(ref _isHighlightOverlayVisible, value);
    }

    public ReplayIndicatorInfo? ActiveReplayIndicator
    {
        get => _activeReplayIndicator;
        set => RaiseAndSetIfChanged(ref _activeReplayIndicator, value);
    }

    public string NavigateUrlText
    {
        get => _navigateUrlText;
        set => RaiseAndSetIfChanged(ref _navigateUrlText, value);
    }

    public bool IsCtrlActive
    {
        get => _isCtrlActive;
        set => RaiseAndSetIfChanged(ref _isCtrlActive, value);
    }

    public bool IsShiftActive
    {
        get => _isShiftActive;
        set => RaiseAndSetIfChanged(ref _isShiftActive, value);
    }

    public bool IsAltActive
    {
        get => _isAltActive;
        set => RaiseAndSetIfChanged(ref _isAltActive, value);
    }

    public bool IsMetaActive
    {
        get => _isMetaActive;
        set => RaiseAndSetIfChanged(ref _isMetaActive, value);
    }

    public string MousePointXText
    {
        get => _mousePointXText;
        set => RaiseAndSetIfChanged(ref _mousePointXText, value);
    }

    public string MousePointYText
    {
        get => _mousePointYText;
        set => RaiseAndSetIfChanged(ref _mousePointYText, value);
    }

    public string MouseDragEndXText
    {
        get => _mouseDragEndXText;
        set => RaiseAndSetIfChanged(ref _mouseDragEndXText, value);
    }

    public string MouseDragEndYText
    {
        get => _mouseDragEndYText;
        set => RaiseAndSetIfChanged(ref _mouseDragEndYText, value);
    }

    public string MouseButtonText
    {
        get => _mouseButtonText;
        set => RaiseAndSetIfChanged(ref _mouseButtonText, value);
    }

    public string MouseClickCountText
    {
        get => _mouseClickCountText;
        set => RaiseAndSetIfChanged(ref _mouseClickCountText, value);
    }

    public List<string> KeysList { get; } = new()
    {
        "Enter", "Tab", "Escape", "Space", "Backspace", "Delete",
        "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown",
        "PageUp", "PageDown", "Home", "End"
    };

    public List<string> MouseButtonsList { get; } = new()
    {
        "left", "right", "middle"
    };

    public ICommand ClickCommand { get; }
    public ICommand SendTextCommand { get; }
    public ICommand SendKeyCommand { get; }
    public ICommand ScrollCommand { get; }
    public ICommand ResizeCommand { get; }
    public ICommand ResizeResetCommand { get; }
    public ICommand CaptureScreenshotCommand { get; }
    
    // Page
    public ICommand NavigateCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }

    // Mouse
    public ICommand MouseMoveCommand { get; }
    public ICommand MouseClickAtPointCommand { get; }
    public ICommand MouseDragCommand { get; }
    public ICommand RotateDeviceCommand { get; }

    // Zoom & Pan & Window Frame
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomResetCommand { get; }
    public ICommand ToggleFitZoomCommand { get; }
    public ICommand ResetPanCommand { get; }
    public ICommand TogglePanModeCommand { get; }
    public ICommand ToggleWindowFrameCommand { get; }
    public ICommand VirtualMinimizeCommand { get; }
    public ICommand VirtualMaximizeCommand { get; }
    public ICommand VirtualCloseCommand { get; }

    public SimulationViewModel(
        ICdpService cdpService,
        Func<DomNodeModel?> getSelectedNodeFunc,
        Func<bool> isHighlightActiveFunc,
        Func<int, (string? Role, string? Name)> getAxDetailsFunc,
        Func<bool> isInspectModeActiveFunc,
        Func<int, DomNodeModel?> getDomNodeFunc,
        Func<bool> useAutomationSelectorsFunc)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _getSelectedNodeFunc = getSelectedNodeFunc ?? throw new ArgumentNullException(nameof(getSelectedNodeFunc));
        _isHighlightActiveFunc = isHighlightActiveFunc ?? throw new ArgumentNullException(nameof(isHighlightActiveFunc));
        _getAxDetailsFunc = getAxDetailsFunc ?? throw new ArgumentNullException(nameof(getAxDetailsFunc));
        _isInspectModeActiveFunc = isInspectModeActiveFunc ?? throw new ArgumentNullException(nameof(isInspectModeActiveFunc));
        _getDomNodeFunc = getDomNodeFunc ?? throw new ArgumentNullException(nameof(getDomNodeFunc));
        _useAutomationSelectorsFunc = useAutomationSelectorsFunc ?? (() => false);

        _selectedDevicePreset = _devicePresets[0];

        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        ClickCommand = new RelayCommand(async () => await ClickSelectedNodeAsync(), () => _cdpService.IsConnected && _getSelectedNodeFunc() != null);
        SendTextCommand = new RelayCommand(async () => await SendTextAsync(), () => _cdpService.IsConnected && !string.IsNullOrEmpty(InputSimText));
        SendKeyCommand = new RelayCommand(async () => await SendKeyAsync(), () => _cdpService.IsConnected && !string.IsNullOrEmpty(SelectedKey));
        ScrollCommand = new RelayCommand(async () => await ScrollAsync(), () => _cdpService.IsConnected);
        ResizeCommand = new RelayCommand(async () => await ResizeAsync(), () => _cdpService.IsConnected);
        ResizeResetCommand = new RelayCommand(async () => await ResizeResetAsync(), () => _cdpService.IsConnected);
        CaptureScreenshotCommand = new RelayCommand(async () => await CaptureScreenshotAsync(), () => _cdpService.IsConnected);

        NavigateCommand = new RelayCommand(async () => await NavigateAsync(), () => _cdpService.IsConnected);
        ReloadCommand = new RelayCommand(async () => await ReloadAsync(), () => _cdpService.IsConnected);
        BackCommand = new RelayCommand(async () => await GoBackAsync(), () => _cdpService.IsConnected);
        ForwardCommand = new RelayCommand(async () => await GoForwardAsync(), () => _cdpService.IsConnected);
        RotateDeviceCommand = new RelayCommand(async () => await RotateDeviceAsync(), () => _cdpService.IsConnected);

        MouseMoveCommand = new RelayCommand(async () => await MouseMoveAsync(), () => _cdpService.IsConnected);
        MouseClickAtPointCommand = new RelayCommand(async () => await MouseClickAtPointAsync(), () => _cdpService.IsConnected);
        MouseDragCommand = new RelayCommand(async () => await MouseDragAsync(), () => _cdpService.IsConnected);

        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ZoomResetCommand = new RelayCommand(ZoomReset);
        ToggleFitZoomCommand = new RelayCommand(ToggleFitZoom);
        ResetPanCommand = new RelayCommand(ResetPan);
        TogglePanModeCommand = new RelayCommand(() => IsPanModeActive = !IsPanModeActive);
        ToggleWindowFrameCommand = new RelayCommand(() => IsWindowFrameVisible = !IsWindowFrameVisible);
        VirtualMinimizeCommand = new RelayCommand(async () => await ExecuteVirtualWindowControlAsync("minimize"), () => _cdpService.IsConnected);
        VirtualMaximizeCommand = new RelayCommand(async () => await ExecuteVirtualWindowControlAsync("maximize"), () => _cdpService.IsConnected);
        VirtualCloseCommand = new RelayCommand(async () => await ExecuteVirtualWindowControlAsync("close"), () => _cdpService.IsConnected);

        _cdpService.EventReceived += CdpService_EventReceived;
        if (_cdpService.IsConnected)
        {
            _ = StartScreencastAsync();
        }
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Logger.LogPlaywrightDebug($"CdpService_PropertyChanged: {e.PropertyName}, IsConnected: {_cdpService.IsConnected}");
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
                ClearData();
            }
            else
            {
                _ = StartScreencastAsync();
            }
            RaiseCanExecuteChangedForAll();
        }
    }

    public void RaiseCanExecuteChangedForAll()
    {
        ((RelayCommand)ClickCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SendTextCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SendKeyCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ScrollCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ResizeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ResizeResetCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CaptureScreenshotCommand).RaiseCanExecuteChanged();

        ((RelayCommand)NavigateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ForwardCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RotateDeviceCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MouseMoveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MouseClickAtPointCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MouseDragCommand).RaiseCanExecuteChanged();
    }

    private int GetCombinedModifiers()
    {
        int modifiers = 0;
        if (IsAltActive) modifiers |= 1;
        if (IsCtrlActive) modifiers |= 2;
        if (IsMetaActive) modifiers |= 4;
        if (IsShiftActive) modifiers |= 8;
        return modifiers;
    }

    private async Task ClickSelectedNodeAsync()
    {
        var selectedNode = _getSelectedNodeFunc();
        if (selectedNode == null) return;

        try
        {
            var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = selectedNode.NodeId });
            var model = boxRes["model"] as JsonObject;
            var contentQuad = model?["content"] as JsonArray;
            if (contentQuad != null && contentQuad.Count >= 8)
            {
                double x1 = contentQuad[0]!.GetValue<double>();
                double y1 = contentQuad[1]!.GetValue<double>();
                double x3 = contentQuad[4]!.GetValue<double>();
                double y3 = contentQuad[5]!.GetValue<double>();
                double cx = (x1 + x3) / 2;
                double cy = (y1 + y3) / 2;

                int modifiers = GetCombinedModifiers();

                await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                {
                    ["type"] = "mousePressed",
                    ["x"] = cx,
                    ["y"] = cy,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["modifiers"] = modifiers
                });
                await Task.Delay(50);
                await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                {
                    ["type"] = "mouseReleased",
                    ["x"] = cx,
                    ["y"] = cy,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["modifiers"] = modifiers
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Click failed", ex);
        }
    }

    private async Task SendTextAsync()
    {
        string text = InputSimText;
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = text });
            InputSimText = "";
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Text input failed", ex);
        }
    }

    private async Task SendKeyAsync()
    {
        string key = SelectedKey;
        if (string.IsNullOrEmpty(key)) return;
        int modifiers = GetCombinedModifiers();
        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = "rawKeyDown",
                ["key"] = key,
                ["modifiers"] = modifiers
            });
            await Task.Delay(50);
            await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = "keyUp",
                ["key"] = key,
                ["modifiers"] = modifiers
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Keystroke simulation failed", ex);
        }
    }

    private async Task ScrollAsync()
    {
        double cx = 100;
        double cy = 100;
        var selectedNode = _getSelectedNodeFunc();
        if (selectedNode != null)
        {
            try
            {
                var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = selectedNode.NodeId });
                var model = boxRes["model"] as JsonObject;
                var contentQuad = model?["content"] as JsonArray;
                if (contentQuad != null && contentQuad.Count >= 8)
                {
                    double x1 = contentQuad[0]!.GetValue<double>();
                    double y1 = contentQuad[1]!.GetValue<double>();
                    double x3 = contentQuad[4]!.GetValue<double>();
                    double y3 = contentQuad[5]!.GetValue<double>();
                    cx = (x1 + x3) / 2;
                    cy = (y1 + y3) / 2;
                }
            }
            catch { }
        }

        if (!double.TryParse(ScrollDeltaYText, out double deltaY))
        {
            deltaY = 100;
        }

        int modifiers = GetCombinedModifiers();

        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseWheel",
                ["x"] = cx,
                ["y"] = cy,
                ["deltaX"] = 0.0,
                ["deltaY"] = deltaY,
                ["modifiers"] = modifiers
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Scroll failed", ex);
        }
    }

    private async Task ResizeAsync()
    {
        if (!int.TryParse(WidthText, out int w) || !int.TryParse(HeightText, out int h)) return;
        if (!double.TryParse(ScaleFactorText, out double scale)) scale = 1.0;
        bool mobile = IsMobileActive;

        DeviceWidth = w;
        DeviceHeight = h;

        try
        {
            await _cdpService.SendCommandAsync("Emulation.setDeviceMetricsOverride", new JsonObject
            {
                ["width"] = w,
                ["height"] = h,
                ["deviceScaleFactor"] = scale,
                ["mobile"] = mobile
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Resize failed", ex);
        }
    }

    private async Task ResizeResetAsync()
    {
        WidthText = "800";
        HeightText = "600";
        ScaleFactorText = "1.0";
        IsMobileActive = false;
        DeviceWidth = 800;
        DeviceHeight = 600;

        try
        {
            await _cdpService.SendCommandAsync("Emulation.clearDeviceMetricsOverride", new JsonObject());
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Reset resize failed", ex);
        }
    }

    private async Task CaptureScreenshotAsync()
    {
        try
        {
            var res = await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
            string base64 = res["data"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(base64))
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                Dispatcher.UIThread.Post(() =>
                {
                    ScreenshotImage = bitmap;
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Screenshot capture failed", ex);
        }
    }

    private async Task NavigateAsync()
    {
        string url = NavigateUrlText;
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            await _cdpService.SendCommandAsync("Page.navigate", new JsonObject { ["url"] = url });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Navigation failed", ex);
        }
    }

    private async Task ReloadAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Page.reload", new JsonObject());
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Reload failed", ex);
        }
    }

    private async Task GoBackAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            var res = await _cdpService.SendCommandAsync("Page.getNavigationHistory", new JsonObject());
            if (res != null && res.ContainsKey("entries") && res.ContainsKey("currentIndex"))
            {
                var entries = res["entries"] as JsonArray;
                int currentIndex = res["currentIndex"]?.GetValue<int>() ?? -1;
                if (entries != null && currentIndex > 0)
                {
                    var targetEntry = entries[currentIndex - 1] as JsonObject;
                    int id = targetEntry?["id"]?.GetValue<int>() ?? -1;
                    if (id != -1)
                    {
                        await _cdpService.SendCommandAsync("Page.navigateToHistoryEntry", new JsonObject { ["entryId"] = id });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "GoBack failed", ex);
        }
    }

    private async Task GoForwardAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            var res = await _cdpService.SendCommandAsync("Page.getNavigationHistory", new JsonObject());
            if (res != null && res.ContainsKey("entries") && res.ContainsKey("currentIndex"))
            {
                var entries = res["entries"] as JsonArray;
                int currentIndex = res["currentIndex"]?.GetValue<int>() ?? -1;
                if (entries != null && currentIndex >= 0 && currentIndex < entries.Count - 1)
                {
                    var targetEntry = entries[currentIndex + 1] as JsonObject;
                    int id = targetEntry?["id"]?.GetValue<int>() ?? -1;
                    if (id != -1)
                    {
                        await _cdpService.SendCommandAsync("Page.navigateToHistoryEntry", new JsonObject { ["entryId"] = id });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "GoForward failed", ex);
        }
    }

    private async Task RotateDeviceAsync()
    {
        if (!_cdpService.IsConnected) return;
        if (int.TryParse(WidthText, out int w) && int.TryParse(HeightText, out int h))
        {
            WidthText = h.ToString();
            HeightText = w.ToString();
            await ResizeAsync();
        }
    }

    private async Task MouseMoveAsync()
    {
        if (!double.TryParse(MousePointXText, out double x) || !double.TryParse(MousePointYText, out double y)) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "none"
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Mouse move failed", ex);
        }
    }

    private async Task MouseClickAtPointAsync()
    {
        if (!double.TryParse(MousePointXText, out double x) || !double.TryParse(MousePointYText, out double y)) return;
        if (!int.TryParse(MouseClickCountText, out int clickCount)) clickCount = 1;
        string button = MouseButtonText;
        int modifiers = GetCombinedModifiers();
        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = clickCount,
                ["modifiers"] = modifiers
            });
            await Task.Delay(50);
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = clickCount,
                ["modifiers"] = modifiers
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Click at point failed", ex);
        }
    }

    private async Task MouseDragAsync()
    {
        if (!double.TryParse(MousePointXText, out double x) || !double.TryParse(MousePointYText, out double y)) return;
        if (!double.TryParse(MouseDragEndXText, out double endX) || !double.TryParse(MouseDragEndYText, out double endY)) return;
        string button = MouseButtonText;
        int modifiers = GetCombinedModifiers();
        try
        {
            // Move to start point
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "none",
                ["buttons"] = 0
            });
            await Task.Delay(50);
            
            int buttons = 0;
            if (button == "left") buttons = 1;
            else if (button == "right") buttons = 2;
            else if (button == "middle") buttons = 4;

            // Press button
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = 1,
                ["modifiers"] = modifiers,
                ["buttons"] = buttons
            });
            await Task.Delay(50);

            // Move to end point (with button drag state represented in modifiers)
            int dragModifiers = modifiers;
            if (button == "left") dragModifiers |= 16;
            else if (button == "right") dragModifiers |= 32;
            else if (button == "middle") dragModifiers |= 64;

            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = button,
                ["modifiers"] = dragModifiers,
                ["buttons"] = buttons
            });
            await Task.Delay(50);

            // Release button
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = button,
                ["clickCount"] = 1,
                ["modifiers"] = modifiers,
                ["buttons"] = 0
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Drag failed", ex);
        }
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ScreenshotImage = null;
            _tiledBitmap1?.Dispose();
            _tiledBitmap2?.Dispose();
            _tiledBitmap1 = null;
            _tiledBitmap2 = null;
            _lastColorType = SkiaSharp.SKColorType.Unknown;
            InputSimText = "";
            IsCtrlActive = false;
            IsShiftActive = false;
            IsAltActive = false;
            IsMetaActive = false;
        });
    }

    private async Task UpdateTiledScreenshotAsync(int width, int height)
    {
        var reconstructor = _cdpService.ScreencastReconstructor;
        var backing = reconstructor.BackingBitmap;
        if (backing == null) return;

        var colorType = backing.ColorType;

        if (_tiledBitmap1 == null || 
            _tiledBitmap1.PixelSize.Width != width || 
            _tiledBitmap1.PixelSize.Height != height ||
            _lastColorType != colorType)
        {
            _tiledBitmap1?.Dispose();
            _tiledBitmap2?.Dispose();

            _lastColorType = colorType;
            var format = colorType == SkiaSharp.SKColorType.Rgba8888
                ? Avalonia.Platform.PixelFormat.Rgba8888
                : Avalonia.Platform.PixelFormat.Bgra8888;

            _tiledBitmap1 = new WriteableBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96),
                format,
                Avalonia.Platform.AlphaFormat.Premul);

            _tiledBitmap2 = new WriteableBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96),
                format,
                Avalonia.Platform.AlphaFormat.Premul);
        }

        var targetBitmap = _useBitmap1 ? _tiledBitmap1 : _tiledBitmap2;
        _useBitmap1 = !_useBitmap1;

        bool copied = false;
        using (var locked = targetBitmap.Lock())
        {
            copied = reconstructor.CopyTo(locked.Address, locked.RowBytes, width, height);
        }

        if (copied)
        {
            ScreenshotImage = targetBitmap;
            await TriggerHighlightRefreshAsync();
        }
    }

    private void OnDevicePresetChanged()
    {
        if (SelectedDevicePreset == null) return;

        if (SelectedDevicePreset.Width == 0 && SelectedDevicePreset.Height == 0)
        {
            WidthText = "800";
            HeightText = "600";
            ScaleFactorText = "1.0";
            IsMobileActive = false;
            _ = ResizeResetAsync();
        }
        else
        {
            WidthText = SelectedDevicePreset.Width.ToString();
            HeightText = SelectedDevicePreset.Height.ToString();
            ScaleFactorText = SelectedDevicePreset.Scale.ToString();
            IsMobileActive = SelectedDevicePreset.IsMobile;
            _ = ResizeAsync();
        }
    }

    private async Task<string> QueryCurrentUrlAsync()
    {
        try
        {
            var historyRes = await _cdpService.SendCommandAsync("Page.getNavigationHistory", new JsonObject());
            var entries = historyRes["entries"] as JsonArray;
            int currentIndex = historyRes["currentIndex"]?.GetValue<int>() ?? -1;
            if (entries != null && currentIndex >= 0 && currentIndex < entries.Count)
            {
                var currentEntry = entries[currentIndex] as JsonObject;
                var url = currentEntry?["url"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrEmpty(url)) return url;
            }
        }
        catch { }

        try
        {
            var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject 
            { 
                ["expression"] = "window.location.href",
                ["returnByValue"] = true
            });
            var resultObj = evalRes["result"] as JsonObject;
            var url = resultObj?["value"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(url)) return url;
        }
        catch { }

        return "";
    }

    private async Task StartScreencastAsync()
    {
        Logger.LogScreencastDebug("StartScreencastAsync called!");
        if (!_cdpService.IsConnected) return;
        try
        {
            try
            {
                await _cdpService.SendCommandAsync("Page.enable", new JsonObject());
            }
            catch (Exception ex)
            {
                Logger.LogScreencastError("Page.enable failed", ex);
            }

            if (SelectedDevicePreset != null && SelectedDevicePreset.Width == 0 && SelectedDevicePreset.Height == 0)
            {
                await ResizeResetAsync();
            }
            else
            {
                await ResizeAsync();
            }

            var url = await QueryCurrentUrlAsync();
            if (!string.IsNullOrEmpty(url))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    NavigateUrlText = url;
                });
            }

            Logger.LogScreencastDebug("Sending Page.startScreencast");
            await _cdpService.SendCommandAsync("Page.startScreencast", new JsonObject
            {
                ["format"] = "png",
                ["everyNthFrame"] = 1,
                ["transferMode"] = "tiled"
            });
            _cdpService.IsPreviewScreencastActive = true;
            Logger.LogScreencastDebug("Page.startScreencast sent successfully!");
        }
        catch (Exception ex)
        {
            Logger.LogScreencastError("StartScreencast failed", ex);
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
         if (e.Method == "Page.screencastFrame")
        {
            try
            {
                var base64 = e.Params["data"]?.GetValue<string>() ?? "";
                var sessionId = e.Params["sessionId"]?.GetValue<int>() ?? 0;
                var metadata = e.Params["metadata"];
                
                if (metadata != null)
                {
                    double deviceWidth = metadata["deviceWidth"]?.GetValue<double>() ?? 0;
                    double deviceHeight = metadata["deviceHeight"]?.GetValue<double>() ?? 0;
                    if (deviceWidth > 0 && deviceHeight > 0)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            DeviceWidth = deviceWidth;
                            DeviceHeight = deviceHeight;
                        });
                    }
                }

                var transferMode = e.Params["transferMode"]?.GetValue<string>();

                if (string.Equals(transferMode, "tiled", StringComparison.OrdinalIgnoreCase))
                {
                    int pixelWidth = e.Params["pixelWidth"]?.GetValue<int>() ?? 0;
                    int pixelHeight = e.Params["pixelHeight"]?.GetValue<int>() ?? 0;
                    int tileWidth = e.Params["tileWidth"]?.GetValue<int>() ?? 0;
                    int tileHeight = e.Params["tileHeight"]?.GetValue<int>() ?? 0;
                    var tiles = e.Params["tiles"] as JsonArray;

                    if (pixelWidth > 0 && pixelHeight > 0)
                    {
                        Dispatcher.UIThread.Post(async () =>
                        {
                            try
                            {
                                await UpdateTiledScreenshotAsync(pixelWidth, pixelHeight);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogErrorMessage("SimulationVM", "Error updating tiled preview", ex);
                            }
                        });
                    }
                }
                else
                {
                    byte[]? bytes = null;
                    if (!string.IsNullOrEmpty(base64))
                    {
                        bytes = Convert.FromBase64String(base64);
                    }

                    if (bytes != null && bytes.Length > 0)
                    {
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new Bitmap(ms);
                        Dispatcher.UIThread.Post(async () =>
                        {
                            ScreenshotImage = bitmap;
                            await TriggerHighlightRefreshAsync();
                        });
                    }
                }

                if (sessionId != 0)
                {
                    _ = _cdpService.SendCommandAsync("Page.screencastFrameAck", new JsonObject { ["sessionId"] = sessionId });
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("SimulationVM", "Error processing screencast frame", ex);
            }
        }
        else if (e.Method == "Page.frameNavigated")
        {
            try
            {
                var frameObj = e.Params["frame"] as JsonObject;
                var parentId = frameObj?["parentId"]?.GetValue<string>();
                if (string.IsNullOrEmpty(parentId)) // top-level frame
                {
                    var url = frameObj?["url"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(url))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            NavigateUrlText = url;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("SimulationVM", "Error processing frameNavigated", ex);
            }
        }
        else if (e.Method == "Page.navigatedWithinDocument")
        {
            try
            {
                var url = e.Params["url"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrEmpty(url))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        NavigateUrlText = url;
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("SimulationVM", "Error processing navigatedWithinDocument", ex);
            }
        }
    }

    public async Task<string> GetSelectorAtCoordinatesAsync(double x, double y, bool useAutomation)
    {
        try
        {
            var nodeRes = await _cdpService.SendCommandAsync("DOM.getNodeForLocation", new JsonObject
            {
                ["x"] = (int)x,
                ["y"] = (int)y
            });
            var nodeId = nodeRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId > 0)
            {
                var domNode = _getDomNodeFunc(nodeId);
                if (domNode != null)
                {
                    var generator = ClientSelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
                    return generator.GenerateSelector(domNode);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", $"Error getting selector at ({x}, {y})", ex);
        }
        return "";
    }

    public async Task SendMouseEventAsync(string type, double x, double y, string button, int modifiers, int buttons = 0)
    {
        if (!_cdpService.IsConnected) return;

        if (type == "mouseMoved" && _isInspectModeActiveFunc())
        {
            _ = UpdateInspectHoverAsync(x, y);
        }

        int clickCount = 0;
        if (type == "mousePressed" || type == "mouseReleased")
        {
            clickCount = 1;
        }

        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = type,
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = clickCount,
                ["modifiers"] = modifiers,
                ["buttons"] = buttons
            });

            if (type == "mouseReleased")
            {
                _ = Task.Run(async () =>
                {
                    string selector = await GetSelectorAtCoordinatesAsync(x, y, _useAutomationSelectorsFunc());
                    var step = new JsonObject
                    {
                        ["type"] = "click",
                        ["button"] = button,
                        ["clickCount"] = clickCount,
                        ["modifiers"] = modifiers
                    };
                    if (!string.IsNullOrEmpty(selector))
                    {
                        step["selectors"] = new JsonArray { (JsonNode)new JsonArray { (JsonNode?)JsonValue.Create(selector) } };
                    }
                    _lastClickedSelector = selector;
                    InteractionDispatched?.Invoke(this, new InteractionEventArgs(step));
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Interactive mouse event failed", ex);
        }
    }

    public async Task SendWheelEventAsync(double x, double y, double deltaY)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseWheel",
                ["x"] = x,
                ["y"] = y,
                ["deltaX"] = 0.0,
                ["deltaY"] = deltaY * 100.0,
                ["modifiers"] = 0
            });

            _ = Task.Run(async () =>
            {
                string selector = await GetSelectorAtCoordinatesAsync(x, y, _useAutomationSelectorsFunc());
                var step = new JsonObject
                {
                    ["type"] = "scroll",
                    ["deltaX"] = 0,
                    ["deltaY"] = deltaY * 100.0
                };
                if (!string.IsNullOrEmpty(selector))
                {
                    step["selectors"] = new JsonArray { (JsonNode)new JsonArray { (JsonNode?)JsonValue.Create(selector) } };
                }
                InteractionDispatched?.Invoke(this, new InteractionEventArgs(step));
            });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Interactive wheel event failed", ex);
        }
    }

    public async Task SendTextInputAsync(string text)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = text });
            
            var step = new JsonObject
            {
                ["type"] = "change",
                ["value"] = text
            };
            if (!string.IsNullOrEmpty(_lastClickedSelector))
            {
                step["selectors"] = new JsonArray { (JsonNode)new JsonArray { (JsonNode?)JsonValue.Create(_lastClickedSelector) } };
            }
            InteractionDispatched?.Invoke(this, new InteractionEventArgs(step));
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Interactive text input failed", ex);
        }
    }

    public async Task SendKeyboardEventAsync(string type, string key, int modifiers)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = type,
                ["key"] = key,
                ["modifiers"] = modifiers
            });

            if (type == "rawKeyDown")
            {
                var step = new JsonObject
                {
                    ["type"] = "keydown",
                    ["key"] = key,
                    ["modifiers"] = modifiers
                };
                InteractionDispatched?.Invoke(this, new InteractionEventArgs(step));
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", "Interactive key event failed", ex);
        }
    }

    public async Task TriggerHighlightRefreshAsync()
    {
        if (_isInspectModeActiveFunc()) return;

        var selectedNode = _getSelectedNodeFunc();
        bool isHighlightActive = _isHighlightActiveFunc();
        if (selectedNode == null || !isHighlightActive || !_cdpService.IsConnected)
        {
            HighlightBoxModel = null;
            HighlightElementType = null;
            HighlightAxRole = null;
            HighlightAxName = null;
            IsHighlightOverlayVisible = false;
            return;
        }

        try
        {
            var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = selectedNode.NodeId });
            
            // Re-validate selection and highlight active state to avoid race conditions or stale overrides
            var currentNode = _getSelectedNodeFunc();
            var isHighlightActiveNow = _isHighlightActiveFunc();
            if (currentNode != selectedNode || !isHighlightActiveNow || !_cdpService.IsConnected)
            {
                if (currentNode == null || !isHighlightActiveNow)
                {
                    HighlightBoxModel = null;
                    HighlightElementType = null;
                    HighlightAxRole = null;
                    HighlightAxName = null;
                    IsHighlightOverlayVisible = false;
                }
                return;
            }

            var model = boxRes["model"] as JsonObject;
            if (model != null)
            {
                var axDetails = _getAxDetailsFunc(selectedNode.NodeId);
                HighlightElementType = selectedNode.NodeName;
                HighlightAxRole = axDetails.Role;
                HighlightAxName = axDetails.Name;
                HighlightBoxModel = model;
                IsHighlightOverlayVisible = true;
            }
            else
            {
                HighlightBoxModel = null;
                IsHighlightOverlayVisible = false;
            }
        }
        catch
        {
            HighlightBoxModel = null;
            IsHighlightOverlayVisible = false;
        }
    }
    public async Task UpdateInspectHoverAsync(double x, double y)
    {
        if (!_cdpService.IsConnected) return;

        if (_isInspectQueryInFlight)
        {
            _pendingInspectX = x;
            _pendingInspectY = y;
            _hasPendingInspect = true;
            return;
        }

        // Throttle: only query if mouse moved by at least 2 pixels
        if (Math.Abs(x - _lastInspectX) < 2 && Math.Abs(y - _lastInspectY) < 2) return;

        _lastInspectX = x;
        _lastInspectY = y;
        _isInspectQueryInFlight = true;
        int currentGen = _hoverGeneration;

        try
        {
            var nodeRes = await _cdpService.SendCommandAsync("DOM.getNodeForLocation", new JsonObject
            {
                ["x"] = (int)x,
                ["y"] = (int)y
            });
            
            // Re-validate inspect mode active state and generation
            if (currentGen != _hoverGeneration || !_isInspectModeActiveFunc() || !_cdpService.IsConnected)
            {
                ClearInspectHover();
                return;
            }

            var nodeId = nodeRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId > 0)
            {
                var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                
                // Re-validate inspect mode active state and generation
                if (currentGen != _hoverGeneration || !_isInspectModeActiveFunc() || !_cdpService.IsConnected)
                {
                    ClearInspectHover();
                    return;
                }

                var model = boxRes["model"] as JsonObject;
                if (model != null)
                {
                    var domNode = _getDomNodeFunc(nodeId);
                    var axDetails = _getAxDetailsFunc(nodeId);

                    HighlightElementType = domNode?.NodeName ?? "Visual";
                    HighlightAxRole = axDetails.Role;
                    HighlightAxName = axDetails.Name;
                    HighlightBoxModel = model;
                    IsHighlightOverlayVisible = true;
                }
                else
                {
                    ClearInspectHover();
                }
            }
            else
            {
                ClearInspectHover();
            }
        }
        catch (Exception)
        {
            ClearInspectHover();
        }
        finally
        {
            _isInspectQueryInFlight = false;
            if (currentGen == _hoverGeneration && _hasPendingInspect && _cdpService.IsConnected && _isInspectModeActiveFunc())
            {
                double px = _pendingInspectX;
                double py = _pendingInspectY;
                _hasPendingInspect = false;
                _ = UpdateInspectHoverAsync(px, py);
            }
        }
    }

    public void ClearInspectHover()
    {
        _hoverGeneration++;
        HighlightBoxModel = null;
        HighlightElementType = null;
        HighlightAxRole = null;
        HighlightAxName = null;
        IsHighlightOverlayVisible = false;
    }

    public void ResetInspectHoverCache()
    {
        _hoverGeneration++;
        _lastInspectX = -999;
        _lastInspectY = -999;
    }

    public void ZoomIn()
    {
        IsFitZoomActive = false;
        ZoomLevel = Math.Min(5.0, ZoomLevel * 1.25);
        _selectedZoomPreset = $"{ZoomLevel * 100:F0}%";
        OnPropertyChanged(nameof(SelectedZoomPreset));
    }

    public void ZoomOut()
    {
        IsFitZoomActive = false;
        ZoomLevel = Math.Max(0.1, ZoomLevel / 1.25);
        _selectedZoomPreset = $"{ZoomLevel * 100:F0}%";
        OnPropertyChanged(nameof(SelectedZoomPreset));
    }

    public void ZoomReset()
    {
        IsFitZoomActive = false;
        ZoomLevel = 1.0;
        PanX = 0;
        PanY = 0;
        _selectedZoomPreset = "100%";
        OnPropertyChanged(nameof(SelectedZoomPreset));
    }

    public void ToggleFitZoom()
    {
        IsFitZoomActive = !IsFitZoomActive;
        if (IsFitZoomActive)
        {
            PanX = 0;
            PanY = 0;
            _selectedZoomPreset = "Fit";
            OnPropertyChanged(nameof(SelectedZoomPreset));
        }
    }

    public void ResetPan()
    {
        PanX = 0;
        PanY = 0;
    }

    public void ApplyZoomPreset(string preset)
    {
        if (string.IsNullOrEmpty(preset)) return;
        if (preset.Equals("Fit", StringComparison.OrdinalIgnoreCase))
        {
            IsFitZoomActive = true;
            PanX = 0;
            PanY = 0;
            return;
        }

        IsFitZoomActive = false;
        string clean = preset.Replace("%", "").Trim();
        if (double.TryParse(clean, out double pct))
        {
            ZoomLevel = pct / 100.0;
        }
    }

    public async Task ResizeToMetricsAsync(int width, int height)
    {
        WidthText = Math.Max(100, width).ToString();
        HeightText = Math.Max(100, height).ToString();
        await ResizeAsync();
    }

    private async Task ExecuteVirtualWindowControlAsync(string action)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            string expression = action switch
            {
                "minimize" => "if (Window != null) Window.WindowState = Avalonia.Controls.WindowState.Minimized;",
                "maximize" => "if (Window != null) Window.WindowState = (Window.WindowState == Avalonia.Controls.WindowState.Maximized) ? Avalonia.Controls.WindowState.Normal : Avalonia.Controls.WindowState.Maximized;",
                "close" => "if (Window != null) Window.Close();",
                _ => ""
            };

            if (!string.IsNullOrEmpty(expression))
            {
                await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = expression,
                    ["returnByValue"] = true
                });
                await RefreshWindowMetadataAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SimulationVM", $"Virtual window action {action} failed", ex);
        }
    }

    public async Task RefreshWindowMetadataAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "(Window?.Title ?? document.title) + '|' + (Window?.WindowState.ToString() ?? 'Normal')",
                ["returnByValue"] = true
            });
            var resultObj = evalRes["result"] as JsonObject;
            var val = resultObj?["value"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(val))
            {
                var parts = val.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    Dispatcher.UIThread.Post(() => WindowTitle = parts[0]);
                }
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    Dispatcher.UIThread.Post(() => WindowStateText = parts[1]);
                }
            }
        }
        catch { }
    }

    #region IStateProvider Implementation

    public string StateKey => "simulation";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["selectedDevicePresetName"] = SelectedDevicePreset?.DisplayName;
        root["widthText"] = WidthText;
        root["heightText"] = HeightText;
        root["scaleFactorText"] = ScaleFactorText;
        root["isMobileActive"] = IsMobileActive;
        root["navigateUrlText"] = NavigateUrlText;
        root["isAdvancedToolsExpanded"] = IsAdvancedToolsExpanded;
        root["isAdvancedToolsPinned"] = IsAdvancedToolsPinned;
        root["advancedToolsPanelHeight"] = AdvancedToolsPanelHeight;
        root["zoomLevel"] = ZoomLevel;
        root["isFitZoomActive"] = IsFitZoomActive;
        root["selectedZoomPreset"] = SelectedZoomPreset;
        root["panX"] = PanX;
        root["panY"] = PanY;
        root["isPanModeActive"] = IsPanModeActive;
        root["isWindowFrameVisible"] = IsWindowFrameVisible;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("selectedDevicePresetName", out var presetNode) && presetNode != null)
        {
            var presetName = (string?)presetNode;
            var matchedPreset = DevicePresets.FirstOrDefault(p => p.DisplayName == presetName);
            if (matchedPreset != null)
            {
                SelectedDevicePreset = matchedPreset;
            }
        }

        if (json.TryGetPropertyValue("widthText", out var wNode) && wNode != null)
        {
            WidthText = (string?)wNode ?? "800";
        }
        if (json.TryGetPropertyValue("heightText", out var hNode) && hNode != null)
        {
            HeightText = (string?)hNode ?? "600";
        }
        if (json.TryGetPropertyValue("scaleFactorText", out var scaleNode) && scaleNode != null)
        {
            ScaleFactorText = (string?)scaleNode ?? "1.0";
        }
        if (json.TryGetPropertyValue("isMobileActive", out var mobNode) && mobNode != null)
        {
            IsMobileActive = (bool?)mobNode ?? false;
        }
        if (json.TryGetPropertyValue("navigateUrlText", out var urlNode) && urlNode != null)
        {
            NavigateUrlText = (string?)urlNode ?? "http://localhost:9222/about";
        }
        if (json.TryGetPropertyValue("isAdvancedToolsExpanded", out var expNode) && expNode != null)
        {
            IsAdvancedToolsExpanded = (bool?)expNode ?? false;
        }
        if (json.TryGetPropertyValue("isAdvancedToolsPinned", out var pinNode) && pinNode != null)
        {
            IsAdvancedToolsPinned = (bool?)pinNode ?? true;
        }
        if (json.TryGetPropertyValue("advancedToolsPanelHeight", out var hPanelNode) && hPanelNode != null)
        {
            AdvancedToolsPanelHeight = (double?)hPanelNode ?? 220.0;
        }
        if (json.TryGetPropertyValue("zoomLevel", out var zNode) && zNode != null)
        {
            ZoomLevel = (double?)zNode ?? 1.0;
        }
        if (json.TryGetPropertyValue("isFitZoomActive", out var fitNode) && fitNode != null)
        {
            IsFitZoomActive = (bool?)fitNode ?? true;
        }
        if (json.TryGetPropertyValue("selectedZoomPreset", out var zPNode) && zPNode != null)
        {
            SelectedZoomPreset = (string?)zPNode ?? "Fit";
        }
        if (json.TryGetPropertyValue("panX", out var pxNode) && pxNode != null)
        {
            PanX = (double?)pxNode ?? 0.0;
        }
        if (json.TryGetPropertyValue("panY", out var pyNode) && pyNode != null)
        {
            PanY = (double?)pyNode ?? 0.0;
        }
        if (json.TryGetPropertyValue("isPanModeActive", out var panMNode) && panMNode != null)
        {
            IsPanModeActive = (bool?)panMNode ?? false;
        }
        if (json.TryGetPropertyValue("isWindowFrameVisible", out var wfNode) && wfNode != null)
        {
            IsWindowFrameVisible = (bool?)wfNode ?? true;
        }
    }

    #endregion
}

public class DevicePreset
{
    public string DisplayName { get; }
    public int Width { get; }
    public int Height { get; }
    public double Scale { get; }
    public bool IsMobile { get; }

    public DevicePreset(string displayName, int width, int height, double scale, bool isMobile)
    {
        DisplayName = displayName;
        Width = width;
        Height = height;
        Scale = scale;
        IsMobile = isMobile;
    }
}

public class InteractionEventArgs : EventArgs
{
    public JsonObject Step { get; }
    public InteractionEventArgs(JsonObject step)
    {
        Step = step;
    }
}

public class TargetViewItemViewModel : ViewModelBase
{
    public string Id { get; }
    public string Title { get; }

    public TargetViewItemViewModel(string id, string title)
    {
        Id = id;
        Title = title;
    }
}
