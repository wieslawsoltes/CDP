using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private string _hostAddress = "http://127.0.0.1:9222";
    private ObservableCollection<TargetItem> _targets = new();
    private TargetItem? _selectedTarget;
    private bool _isInspectModeActive;
    private bool _useAutomationSelectors;

    public string HostAddress
    {
        get => _hostAddress;
        set => RaiseAndSetIfChanged(ref _hostAddress, value);
    }

    public bool UseAutomationSelectors
    {
        get => _useAutomationSelectors;
        set => RaiseAndSetIfChanged(ref _useAutomationSelectors, value);
    }

    public ObservableCollection<TargetItem> Targets
    {
        get => _targets;
        private set => RaiseAndSetIfChanged(ref _targets, value);
    }

    public TargetItem? SelectedTarget
    {
        get => _selectedTarget;
        set => RaiseAndSetIfChanged(ref _selectedTarget, value);
    }

    public bool IsConnected => _cdpService.IsConnected;
    public bool IsNotConnected => !_cdpService.IsConnected;

    public string ConnectionStatusText => _cdpService.ConnectionStatus;

    public IBrush ConnectionStatusBrush => ConnectionStatusText.ToLowerInvariant() switch
    {
        "connected" => Brushes.LightGreen,
        "connecting..." => Brushes.Orange,
        "connection failed" => Brushes.Red,
        "disconnecting..." => Brushes.Orange,
        _ => Brushes.LightCoral
    };

    public bool IsInspectModeActive
    {
        get => _isInspectModeActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isInspectModeActive, value))
            {
                _ = ToggleInspectModeAsync();
            }
        }
    }

    public ICommand RefreshTargetsCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ReloadCommand { get; }

    // Window control commands
    public ICommand RefreshWindowDetailsCommand { get; }
    public ICommand ApplyWindowBoundsCommand { get; }
    public ICommand ApplyWindowOpacityCommand { get; }
    public ICommand ApplyWindowTitleCommand { get; }
    public ICommand ApplyWindowTopmostCommand { get; }
    public ICommand MinimizeWindowCommand { get; }
    public ICommand MaximizeWindowCommand { get; }
    public ICommand RestoreWindowCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand ActivateWindowCommand { get; }
    public ICommand DragWindowCommand { get; }

    public ConnectionViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;
        this.PropertyChanged += ConnectionViewModel_PropertyChanged;

        RefreshTargetsCommand = new RelayCommand(async () => await RefreshTargetsAsync());
        ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => SelectedTarget != null && (IsNotConnected || SelectedTarget.Id != _cdpService.ConnectedTargetId));
        DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => IsConnected);
        ReloadCommand = new RelayCommand(async () => await ReloadAsync(), () => IsConnected);

        RefreshWindowDetailsCommand = new RelayCommand(async () => await RefreshWindowDetailsAsync(), () => IsConnected);
        ApplyWindowBoundsCommand = new RelayCommand(() => ApplyWindowBounds(), () => IsConnected);
        ApplyWindowOpacityCommand = new RelayCommand(() => ApplyWindowOpacity(), () => IsConnected);
        ApplyWindowTitleCommand = new RelayCommand(() => ApplyWindowTitle(), () => IsConnected);
        ApplyWindowTopmostCommand = new RelayCommand(() => ApplyWindowTopmost(), () => IsConnected);
        MinimizeWindowCommand = new RelayCommand(async () => await MinimizeWindowAsync(), () => IsConnected);
        MaximizeWindowCommand = new RelayCommand(async () => await MaximizeWindowAsync(), () => IsConnected);
        RestoreWindowCommand = new RelayCommand(async () => await RestoreWindowAsync(), () => IsConnected);
        CloseWindowCommand = new RelayCommand(async () => await CloseWindowAsync(), () => IsConnected);
        ActivateWindowCommand = new RelayCommand(async () => await ActivateWindowAsync(), () => IsConnected);
        DragWindowCommand = new RelayCommand(async () => await DragWindowAsync(), () => IsConnected);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsNotConnected));
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ReloadCommand).RaiseCanExecuteChanged();

            ((RelayCommand)RefreshWindowDetailsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)MinimizeWindowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)MaximizeWindowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RestoreWindowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CloseWindowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ActivateWindowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DragWindowCommand).RaiseCanExecuteChanged();

            if (IsConnected)
            {
                _ = RefreshWindowDetailsAsync();
            }
        }
        else if (e.PropertyName == nameof(ICdpService.ConnectionStatus))
        {
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(ConnectionStatusBrush));
        }
    }

    private void ConnectionViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedTarget))
        {
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            if (IsConnected && SelectedTarget != null && SelectedTarget.Id != _cdpService.ConnectedTargetId)
            {
                _ = ConnectAsync();
            }
        }
    }

    public async Task RefreshTargetsAsync()
    {
        try
        {
            var list = await _cdpService.GetTargetsAsync(HostAddress);
            Targets.Clear();
            foreach (var target in list)
            {
                Targets.Add(target);
            }
            if (Targets.Count > 0)
            {
                var currentConnected = System.Linq.Enumerable.FirstOrDefault(Targets, t => t.Id == _cdpService.ConnectedTargetId);
                SelectedTarget = currentConnected ?? Targets[0];
            }
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning targets: {ex.Message}");
        }
    }

    public async Task ConnectAsync()
    {
        if (SelectedTarget == null) return;
        try
        {
            await _cdpService.ConnectAsync(HostAddress, SelectedTarget);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connect error: {ex.Message}");
            try
            {
                System.IO.File.WriteAllText("connect_error.txt", ex.ToString());
            }
            catch {}
        }
    }

    public async Task DisconnectAsync()
    {
        IsInspectModeActive = false;
        await _cdpService.DisconnectAsync();
    }

    private async Task ToggleInspectModeAsync()
    {
        if (!IsConnected) return;
        try
        {
            var inspectParams = new JsonObject
            {
                ["mode"] = IsInspectModeActive ? "searchForNode" : "none",
                ["highlightConfig"] = new JsonObject
                {
                    ["showInfo"] = true,
                    ["contentColor"] = new JsonObject { ["r"] = 111, ["g"] = 168, ["b"] = 220, ["a"] = 0.66 }
                }
            };
            await _cdpService.SendCommandAsync("Overlay.setInspectMode", inspectParams);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling inspect mode: {ex.Message}");
        }
    }

    private async Task ReloadAsync()
    {
        if (!IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Page.reload");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reload error: {ex.Message}");
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Target.targetCreated")
        {
            var targetInfo = e.Params["targetInfo"] as JsonObject;
            if (targetInfo != null)
            {
                string id = targetInfo["targetId"]?.GetValue<string>() ?? "";
                string title = targetInfo["title"]?.GetValue<string>() ?? "Unnamed";
                string type = targetInfo["type"]?.GetValue<string>() ?? "";
                
                if (type == "page" || type == "app")
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!System.Linq.Enumerable.Any(Targets, t => t.Id == id))
                        {
                            string wsUrl = targetInfo["webSocketDebuggerUrl"]?.GetValue<string>() ?? "";
                            if (string.IsNullOrEmpty(wsUrl))
                            {
                                try
                                {
                                    var uri = new Uri(HostAddress);
                                    var host = uri.Authority;
                                    wsUrl = $"ws://{host}/devtools/page/{id}";
                                }
                                catch
                                {
                                    wsUrl = $"ws://localhost:9222/devtools/page/{id}";
                                }
                            }
                            Targets.Add(new TargetItem(title, wsUrl, id));
                            if (SelectedTarget == null)
                            {
                                if (_cdpService.IsConnected)
                                {
                                    if (id == _cdpService.ConnectedTargetId)
                                    {
                                        SelectedTarget = Targets[Targets.Count - 1];
                                    }
                                }
                                else
                                {
                                    SelectedTarget = Targets[0];
                                }
                            }
                        }
                    });
                }
            }
        }
        else if (e.Method == "Target.targetDestroyed")
        {
            string id = e.Params["targetId"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(id))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var targetToRemove = System.Linq.Enumerable.FirstOrDefault(Targets, t => t.Id == id);
                    if (targetToRemove != null)
                    {
                        Targets.Remove(targetToRemove);
                        if (SelectedTarget == targetToRemove)
                        {
                            SelectedTarget = Targets.Count > 0 ? Targets[0] : null;
                        }
                    }
                });
            }
        }
        else if (e.Method == "Target.targetInfoChanged")
        {
            var targetInfo = e.Params["targetInfo"] as JsonObject;
            if (targetInfo != null)
            {
                string id = targetInfo["targetId"]?.GetValue<string>() ?? "";
                string title = targetInfo["title"]?.GetValue<string>() ?? "Unnamed";
                if (!string.IsNullOrEmpty(id))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var targetToUpdate = System.Linq.Enumerable.FirstOrDefault(Targets, t => t.Id == id);
                        if (targetToUpdate != null)
                        {
                            int index = Targets.IndexOf(targetToUpdate);
                            Targets[index] = new TargetItem(title, targetToUpdate.WebSocketUrl, id);
                            if (SelectedTarget == targetToUpdate)
                            {
                                SelectedTarget = Targets[index];
                            }
                        }
                    });
                }
            }
        }
    }

    // Window control properties
    private int? _windowId;
    private int _windowX;
    private int _windowY;
    private int _windowWidth;
    private int _windowHeight;
    private bool _isTopmost;
    private double _windowOpacity = 1.0;
    private string _windowTitle = "";
    private string _windowState = "normal";
    private int _dragDeltaX = 50;
    private int _dragDeltaY = 50;
    private bool _isUpdatingFromTarget;

    public int WindowX
    {
        get => _windowX;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowX, value))
            {
                ApplyWindowBounds();
            }
        }
    }

    public int WindowY
    {
        get => _windowY;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowY, value))
            {
                ApplyWindowBounds();
            }
        }
    }

    public int WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowWidth, value))
            {
                ApplyWindowBounds();
            }
        }
    }

    public int WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowHeight, value))
            {
                ApplyWindowBounds();
            }
        }
    }

    public bool IsTopmost
    {
        get => _isTopmost;
        set
        {
            if (RaiseAndSetIfChanged(ref _isTopmost, value))
            {
                ApplyWindowTopmost();
            }
        }
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowOpacity, value))
            {
                ApplyWindowOpacity();
            }
        }
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowTitle, value))
            {
                ApplyWindowTitle();
            }
        }
    }

    public string WindowState
    {
        get => _windowState;
        set => RaiseAndSetIfChanged(ref _windowState, value);
    }

    public int DragDeltaX
    {
        get => _dragDeltaX;
        set => RaiseAndSetIfChanged(ref _dragDeltaX, value);
    }

    public int DragDeltaY
    {
        get => _dragDeltaY;
        set => RaiseAndSetIfChanged(ref _dragDeltaY, value);
    }

    public async Task RefreshWindowDetailsAsync()
    {
        if (!IsConnected) return;
        try
        {
            // 1. Get window for target
            var getWinRes = await _cdpService.SendCommandAsync("Browser.getWindowForTarget", new JsonObject());
            if (getWinRes != null && getWinRes["windowId"] != null)
            {
                int windowId = getWinRes["windowId"]!.GetValue<int>();
                _windowId = windowId;

                // 2. Get window bounds
                var getBoundsRes = await _cdpService.SendCommandAsync("Browser.getWindowBounds", new JsonObject
                {
                    ["windowId"] = windowId
                });

                if (getBoundsRes != null && getBoundsRes["bounds"] is JsonObject bounds)
                {
                    _isUpdatingFromTarget = true;
                    try
                    {
                        _windowX = bounds["left"]?.GetValue<int>() ?? 0;
                        _windowY = bounds["top"]?.GetValue<int>() ?? 0;
                        _windowWidth = bounds["width"]?.GetValue<int>() ?? 0;
                        _windowHeight = bounds["height"]?.GetValue<int>() ?? 0;
                        _windowState = bounds["windowState"]?.GetValue<string>() ?? "normal";
                        
                        OnPropertyChanged(nameof(WindowX));
                        OnPropertyChanged(nameof(WindowY));
                        OnPropertyChanged(nameof(WindowWidth));
                        OnPropertyChanged(nameof(WindowHeight));
                        OnPropertyChanged(nameof(WindowState));
                    }
                    finally
                    {
                        _isUpdatingFromTarget = false;
                    }
                }

                // 3. Get opacity/topmost/title via custom domain
                try
                {
                    var detailsRes = await _cdpService.SendCommandAsync("WindowChrome.getWindowDetails", new JsonObject
                    {
                        ["windowId"] = windowId
                    });
                    if (detailsRes != null && detailsRes["success"]?.GetValue<bool>() == true)
                    {
                        _isUpdatingFromTarget = true;
                        try
                        {
                            _isTopmost = detailsRes["topmost"]?.GetValue<bool>() ?? false;
                            _windowOpacity = detailsRes["opacity"]?.GetValue<double>() ?? 1.0;
                            _windowTitle = detailsRes["title"]?.GetValue<string>() ?? "";
                            
                            OnPropertyChanged(nameof(IsTopmost));
                            OnPropertyChanged(nameof(WindowOpacity));
                            OnPropertyChanged(nameof(WindowTitle));
                        }
                        finally
                        {
                            _isUpdatingFromTarget = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error calling WindowChrome.getWindowDetails: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing window details: {ex.Message}");
        }
    }

    private void ApplyWindowBounds()
    {
        if (_isUpdatingFromTarget || !IsConnected || !_windowId.HasValue) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _cdpService.SendCommandAsync("Browser.setWindowBounds", new JsonObject
                {
                    ["windowId"] = _windowId.Value,
                    ["bounds"] = new JsonObject
                    {
                        ["left"] = WindowX,
                        ["top"] = WindowY,
                        ["width"] = WindowWidth,
                        ["height"] = WindowHeight,
                        ["windowState"] = WindowState
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying window bounds: {ex.Message}");
            }
        });
    }

    private void ApplyWindowTopmost()
    {
        if (_isUpdatingFromTarget || !IsConnected || !_windowId.HasValue) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _cdpService.SendCommandAsync("WindowChrome.setTopmost", new JsonObject
                {
                    ["windowId"] = _windowId.Value,
                    ["topmost"] = IsTopmost
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting topmost: {ex.Message}");
            }
        });
    }

    private void ApplyWindowOpacity()
    {
        if (_isUpdatingFromTarget || !IsConnected || !_windowId.HasValue) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _cdpService.SendCommandAsync("WindowChrome.setOpacity", new JsonObject
                {
                    ["windowId"] = _windowId.Value,
                    ["opacity"] = WindowOpacity
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting opacity: {ex.Message}");
            }
        });
    }

    private void ApplyWindowTitle()
    {
        if (_isUpdatingFromTarget || !IsConnected || !_windowId.HasValue) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _cdpService.SendCommandAsync("WindowChrome.setTitle", new JsonObject
                {
                    ["windowId"] = _windowId.Value,
                    ["title"] = WindowTitle
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting title: {ex.Message}");
            }
        });
    }

    private async Task MinimizeWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.minimize", new JsonObject { ["windowId"] = _windowId.Value });
            WindowState = "minimized";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error minimizing window: {ex.Message}");
        }
    }

    private async Task MaximizeWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.maximize", new JsonObject { ["windowId"] = _windowId.Value });
            WindowState = "maximized";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error maximizing window: {ex.Message}");
        }
    }

    private async Task RestoreWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.restore", new JsonObject { ["windowId"] = _windowId.Value });
            WindowState = "normal";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring window: {ex.Message}");
        }
    }

    private async Task CloseWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.close", new JsonObject { ["windowId"] = _windowId.Value });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing window: {ex.Message}");
        }
    }

    private async Task ActivateWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.activate", new JsonObject { ["windowId"] = _windowId.Value });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error activating window: {ex.Message}");
        }
    }

    private async Task DragWindowAsync()
    {
        if (!IsConnected || !_windowId.HasValue) return;
        try
        {
            await _cdpService.SendCommandAsync("WindowChrome.dragWindow", new JsonObject
            {
                ["windowId"] = _windowId.Value,
                ["deltaX"] = DragDeltaX,
                ["deltaY"] = DragDeltaY
            });
            // Refresh bounds after drag to show new position
            _ = Task.Delay(100).ContinueWith(_ => RefreshWindowDetailsAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dragging window: {ex.Message}");
        }
    }
}
