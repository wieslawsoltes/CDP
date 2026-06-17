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
    private string _hostAddress = "http://localhost:9222";
    private ObservableCollection<TargetItem> _targets = new();
    private TargetItem? _selectedTarget;
    private bool _isInspectModeActive;

    public string HostAddress
    {
        get => _hostAddress;
        set => RaiseAndSetIfChanged(ref _hostAddress, value);
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

    public ConnectionViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        RefreshTargetsCommand = new RelayCommand(async () => await RefreshTargetsAsync());
        ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => SelectedTarget != null && IsNotConnected);
        DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => IsConnected);
        ReloadCommand = new RelayCommand(async () => await ReloadAsync(), () => IsConnected);
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
        }
        else if (e.PropertyName == nameof(ICdpService.ConnectionStatus))
        {
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(ConnectionStatusBrush));
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
                SelectedTarget = Targets[0];
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
}
