using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.ViewModels;

public class ConnectionViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ConnectionViewModel>();
    private readonly ICdpService _cdpService;
    private string _hostAddress = "http://127.0.0.1:9222";
    private string _lastHttpHost = "127.0.0.1:9222";
    private ObservableCollection<TargetItem> _targets = new();
    private TargetItem? _selectedTarget;
    private bool _isInspectModeActive;
    private bool _useAutomationSelectors;
    public ObservableCollection<string> SuggestedHosts { get; } = new()
    {
        "http://127.0.0.1:9222",
        "http://127.0.0.1:9223",
        "os://"
    };

    public string HostAddress
    {
        get => _hostAddress;
        set
        {
            if (RaiseAndSetIfChanged(ref _hostAddress, value))
            {
                UpdateLastHttpHost(value);
                OnPropertyChanged(nameof(GeneratorHostAddress));
            }
        }
    }

    public string GeneratorHostAddress
    {
        get
        {
            if (string.IsNullOrEmpty(HostAddress))
            {
                return "http://127.0.0.1:9222";
            }

            if (HostAddress.StartsWith("os://", StringComparison.OrdinalIgnoreCase))
            {
                return HostAddress;
            }

            if (HostAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                HostAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return HostAddress;
            }

            if (HostAddress.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(HostAddress);
                    return $"http://{uri.Authority}";
                }
                catch {}
            }
            else if (HostAddress.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(HostAddress);
                    return $"https://{uri.Authority}";
                }
                catch {}
            }

            return $"http://{_lastHttpHost}";
        }
    }

    private void UpdateLastHttpHost(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(address);
                _lastHttpHost = uri.Authority;
            }
            catch {}
        }
        else if (address.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || 
                 address.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(address);
                _lastHttpHost = uri.Authority;
            }
            catch {}
        }
        else if (address.Contains(':') || address.Contains('.'))
        {
            _lastHttpHost = address;
        }
    }

    public bool UseAutomationSelectors
    {
        get => _useAutomationSelectors;
        set => RaiseAndSetIfChanged(ref _useAutomationSelectors, value);
    }

    private TestStudioViewModel? _testStudio;
    public TestStudioViewModel? TestStudio
    {
        get => _testStudio;
        set
        {
            if (_testStudio != null)
            {
                _testStudio.PropertyChanged -= TestStudio_PropertyChanged;
            }
            _testStudio = value;
            if (_testStudio != null)
            {
                _testStudio.PropertyChanged += TestStudio_PropertyChanged;
            }
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        }
    }

    private void TestStudio_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioViewModel.IsAutoLaunchEnabled) ||
            e.PropertyName == nameof(TestStudioViewModel.AutoLaunchPath))
        {
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        }
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

    private string _targetSearchText = string.Empty;
    public string TargetSearchText
    {
        get => _targetSearchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _targetSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredTargets));
            }
        }
    }

    public IEnumerable<TargetItem> FilteredTargets
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TargetSearchText))
            {
                return Targets;
            }
            return System.Linq.Enumerable.Where(Targets, t => 
                (t.Title != null && t.Title.Contains(TargetSearchText, StringComparison.OrdinalIgnoreCase)) || 
                (t.Id != null && t.Id.Contains(TargetSearchText, StringComparison.OrdinalIgnoreCase)));
        }
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
        _targets.CollectionChanged += (sender, e) =>
        {
            OnPropertyChanged(nameof(FilteredTargets));
        };

        RefreshTargetsCommand = new RelayCommand(async () => await RefreshTargetsAsync());
        ConnectCommand = new RelayCommand(
            async () => await ConnectAsync(), 
            () => (SelectedTarget != null && (IsNotConnected || SelectedTarget.Id != _cdpService.ConnectedTargetId)) ||
                  (TestStudio != null && TestStudio.IsAutoLaunchEnabled && !string.IsNullOrEmpty(TestStudio.AutoLaunchPath)));
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
        else if (e.PropertyName == nameof(HostAddress))
        {
            UpdateDirectTarget();
        }
    }

    private bool IsDirectHostAddress(string address, out string wsUrl, out string targetId, out string title)
    {
        wsUrl = "";
        targetId = "direct";
        title = "Direct Connection";

        if (string.IsNullOrEmpty(address)) return false;

        bool isWs = address.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || 
                    address.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);

        bool isTargetId = !address.Contains('.') && 
                          !address.Contains(':') && 
                          !address.Contains('/') && 
                          address.Length >= 8;

        if (isWs)
        {
            wsUrl = address;
            try
            {
                var uri = new Uri(address);
                var segments = uri.Segments;
                if (segments.Length > 0)
                {
                    var lastSegment = segments[segments.Length - 1].Trim('/');
                    if (lastSegment.Length >= 8 && !lastSegment.Contains('.'))
                    {
                        targetId = lastSegment;
                    }
                }
            }
            catch {}
            return true;
        }
        else if (isTargetId)
        {
            targetId = address;
            wsUrl = $"ws://{_lastHttpHost}/devtools/page/{targetId}";
            title = $"Direct Target {(targetId.Length > 8 ? targetId.Substring(0, 8) : targetId)}";
            return true;
        }

        return false;
    }

    private void UpdateDirectTarget()
    {
        if (IsDirectHostAddress(HostAddress, out var wsUrl, out var targetId, out var title))
        {
            var directTarget = new TargetItem(title, wsUrl, targetId);
            bool exists = false;
            foreach (var t in Targets)
            {
                if (t.WebSocketUrl == wsUrl)
                {
                    SelectedTarget = t;
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                Targets.Clear();
                Targets.Add(directTarget);
                SelectedTarget = directTarget;
            }
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        }
    }

    public async Task RefreshTargetsAsync()
    {
        try
        {
            var list = await _cdpService.GetTargetsAsync(HostAddress);
            
            bool isDirect = IsDirectHostAddress(HostAddress, out var directWsUrl, out var directTargetId, out var directTitle);
            TargetItem? directTarget = null;
            if (isDirect)
            {
                directTarget = new TargetItem(directTitle, directWsUrl, directTargetId);
            }

            Targets.Clear();
            foreach (var target in list)
            {
                Targets.Add(target);
            }

            if (isDirect && directTarget != null)
            {
                TargetItem? found = null;
                foreach (var t in Targets)
                {
                    if (t.Id == directTarget.Id || t.WebSocketUrl == directTarget.WebSocketUrl)
                    {
                        found = t;
                        break;
                    }
                }

                if (found != null)
                {
                    SelectedTarget = found;
                }
                else
                {
                    Targets.Add(directTarget);
                    SelectedTarget = directTarget;
                }
            }
            else if (Targets.Count > 0)
            {
                var currentConnected = System.Linq.Enumerable.FirstOrDefault(Targets, t => t.Id == _cdpService.ConnectedTargetId);
                SelectedTarget = currentConnected ?? Targets[0];
            }
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ConnectionViewModel", "Error scanning targets", ex);
        }
    }

    public Task ConnectAsync() => ConnectAsync(bypassAutoLaunch: false);

    public async Task ConnectAsync(bool bypassAutoLaunch)
    {
        bool isOsAutomation = GeneratorHostAddress != null && GeneratorHostAddress.StartsWith("os://", StringComparison.OrdinalIgnoreCase);

        if (!isOsAutomation && !bypassAutoLaunch && (SelectedTarget == null || (TestStudio != null && TestStudio.IsAutoLaunchEnabled && !string.IsNullOrEmpty(TestStudio.AutoLaunchPath) && !_cdpService.IsConnected)))
        {
            if (TestStudio != null && TestStudio.IsAutoLaunchEnabled && !string.IsNullOrEmpty(TestStudio.AutoLaunchPath))
            {
                try
                {
                    var launcher = new CdpInspectorApp.Services.AppLauncherService();
                    await launcher.AutoLaunchAppAsync(
                        _cdpService,
                        this,
                        TestStudio.AutoLaunchPath,
                        TestStudio.AutoLaunchArguments,
                        msg => TestStudio.Log(msg),
                        System.Threading.CancellationToken.None);
                    return;
                }
                catch (Exception ex)
                {
                    TestStudio.Log($"Auto Launch Connection Error: {ex.Message}");
                    if (SelectedTarget == null) return;
                }
            }
        }

        if (SelectedTarget == null)
        {
            if (isOsAutomation && TestStudio != null)
            {
                TestStudio.Log("OS Automation: No target process selected. Please select a process window from the targets dropdown before connecting.");
            }
            return;
        }

        try
        {
            if (isOsAutomation && TestStudio != null)
            {
                TestStudio.Log($"OS Automation: Connecting to process window '{SelectedTarget.Title}' (ID: {SelectedTarget.Id})...");
            }

            await _cdpService.ConnectAsync(GeneratorHostAddress, SelectedTarget);

            if (isOsAutomation && TestStudio != null && _cdpService.IsConnected)
            {
                TestStudio.Log($"OS Automation: Successfully connected to '{SelectedTarget.Title}'!");

                if (!CDP.Automation.OS.OSAutomationService.Instance.HasScreenCapturePermission())
                {
                    TestStudio.Log("⚠️ WARNING: macOS Screen Recording permission is NOT granted!");
                    TestStudio.Log("Simulation Preview and screenshots will display blank grey boxes, other windows, or desktop wallpaper.");
                    TestStudio.Log("To fix: Go to System Settings -> Privacy & Security -> Screen Recording, and enable permission for your Terminal, iTerm, or IDE/VS Code.");
                    TestStudio.Log("IMPORTANT: You MUST restart your Terminal, iTerm, or IDE/VS Code after enabling permission for the change to take effect.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("ConnectionViewModel", "Connect error", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error toggling inspect mode", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Reload error", ex);
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
                    Logger.LogWarningMessage("ConnectionViewModel", "Error calling WindowChrome.getWindowDetails", ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ConnectionViewModel", "Error refreshing window details", ex);
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
                Logger.LogWarningMessage("ConnectionViewModel", "Error applying window bounds", ex);
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
                Logger.LogWarningMessage("ConnectionViewModel", "Error setting topmost", ex);
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
                Logger.LogWarningMessage("ConnectionViewModel", "Error setting opacity", ex);
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
                Logger.LogWarningMessage("ConnectionViewModel", "Error setting title", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error minimizing window", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error maximizing window", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error restoring window", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error closing window", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error activating window", ex);
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
            Logger.LogWarningMessage("ConnectionViewModel", "Error dragging window", ex);
        }
    }

    #region IStateProvider Implementation

    public string StateKey => "connection";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["hostAddress"] = HostAddress;
        root["useAutomationSelectors"] = UseAutomationSelectors;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("hostAddress", out var hostNode) && hostNode != null)
        {
            HostAddress = (string?)hostNode ?? "http://127.0.0.1:9222";
        }
        if (json.TryGetPropertyValue("useAutomationSelectors", out var autoNode) && autoNode != null)
        {
            UseAutomationSelectors = (bool?)autoNode ?? false;
        }
    }

    #endregion
}
