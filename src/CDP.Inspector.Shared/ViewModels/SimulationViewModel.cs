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

namespace CdpInspectorApp.ViewModels;

public class SimulationViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly Func<DomNodeModel?> _getSelectedNodeFunc;
    
    private string _inputSimText = "";
    private string _selectedKey = "Enter";
    private string _scrollDeltaYText = "100";
    
    private string _widthText = "800";
    private string _heightText = "600";
    private string _scaleFactorText = "1.0";
    private bool _isMobileActive;
    private Bitmap? _screenshotImage;

    private readonly System.Collections.ObjectModel.ObservableCollection<DevicePreset> _devicePresets = new()
    {
        new DevicePreset("Responsive", 0, 0, 1.0, false),
        new DevicePreset("iPhone SE", 375, 667, 2.0, true),
        new DevicePreset("iPhone 12 Pro", 390, 844, 3.0, true),
        new DevicePreset("Pixel 5", 393, 851, 2.75, true),
        new DevicePreset("iPad Air", 820, 1180, 2.0, true),
        new DevicePreset("Desktop (1080p)", 1920, 1080, 1.0, false)
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
        set => RaiseAndSetIfChanged(ref _isMobileActive, value);
    }

    public Bitmap? ScreenshotImage
    {
        get => _screenshotImage;
        private set => RaiseAndSetIfChanged(ref _screenshotImage, value);
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

    public SimulationViewModel(ICdpService cdpService, Func<DomNodeModel?> getSelectedNodeFunc)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _getSelectedNodeFunc = getSelectedNodeFunc ?? throw new ArgumentNullException(nameof(getSelectedNodeFunc));

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

        _cdpService.EventReceived += CdpService_EventReceived;
        if (_cdpService.IsConnected)
        {
            _ = StartScreencastAsync();
        }
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
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
        if (IsShiftActive) modifiers |= 4;
        if (IsMetaActive) modifiers |= 8;
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
            Console.WriteLine($"Click failed: {ex.Message}");
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
            Console.WriteLine($"Text input failed: {ex.Message}");
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
            Console.WriteLine($"Keystroke simulation failed: {ex.Message}");
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
            Console.WriteLine($"Scroll failed: {ex.Message}");
        }
    }

    private async Task ResizeAsync()
    {
        if (!int.TryParse(WidthText, out int w) || !int.TryParse(HeightText, out int h)) return;
        if (!double.TryParse(ScaleFactorText, out double scale)) scale = 1.0;
        bool mobile = IsMobileActive;
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
            Console.WriteLine($"Resize failed: {ex.Message}");
        }
    }

    private async Task ResizeResetAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Emulation.clearDeviceMetricsOverride", new JsonObject());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reset resize failed: {ex.Message}");
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
            Console.WriteLine($"Screenshot capture failed: {ex.Message}");
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
            Console.WriteLine($"Navigation failed: {ex.Message}");
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
            Console.WriteLine($"Reload failed: {ex.Message}");
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
            Console.WriteLine($"GoBack failed: {ex.Message}");
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
            Console.WriteLine($"GoForward failed: {ex.Message}");
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
            Console.WriteLine($"Mouse move failed: {ex.Message}");
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
            Console.WriteLine($"Click at point failed: {ex.Message}");
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
                ["button"] = "none"
            });
            await Task.Delay(50);
            
            // Press button
            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = 1,
                ["modifiers"] = modifiers
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
                ["modifiers"] = dragModifiers
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
                ["modifiers"] = modifiers
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Drag failed: {ex.Message}");
        }
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ScreenshotImage = null;
            InputSimText = "";
            IsCtrlActive = false;
            IsShiftActive = false;
            IsAltActive = false;
            IsMetaActive = false;
        });
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

    private async Task StartScreencastAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Page.startScreencast", new JsonObject
            {
                ["format"] = "png",
                ["everyNthFrame"] = 1
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StartScreencast failed: {ex.Message}");
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

                if (sessionId != 0)
                {
                    _ = _cdpService.SendCommandAsync("Page.screencastFrameAck", new JsonObject { ["sessionId"] = sessionId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing screencast frame: {ex.Message}");
            }
        }
    }

    public async Task SendMouseEventAsync(string type, double x, double y, string button, int modifiers)
    {
        if (!_cdpService.IsConnected) return;

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
                ["modifiers"] = modifiers
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Interactive mouse event failed: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Interactive wheel event failed: {ex.Message}");
        }
    }

    public async Task SendTextInputAsync(string text)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = text });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Interactive text input failed: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Interactive key event failed: {ex.Message}");
        }
    }
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
