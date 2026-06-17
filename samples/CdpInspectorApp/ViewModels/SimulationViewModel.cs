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

    // Mouse
    public ICommand MouseMoveCommand { get; }
    public ICommand MouseClickAtPointCommand { get; }
    public ICommand MouseDragCommand { get; }

    public SimulationViewModel(ICdpService cdpService, Func<DomNodeModel?> getSelectedNodeFunc)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _getSelectedNodeFunc = getSelectedNodeFunc ?? throw new ArgumentNullException(nameof(getSelectedNodeFunc));

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

        MouseMoveCommand = new RelayCommand(async () => await MouseMoveAsync(), () => _cdpService.IsConnected);
        MouseClickAtPointCommand = new RelayCommand(async () => await MouseClickAtPointAsync(), () => _cdpService.IsConnected);
        MouseDragCommand = new RelayCommand(async () => await MouseDragAsync(), () => _cdpService.IsConnected);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
                ClearData();
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
}
