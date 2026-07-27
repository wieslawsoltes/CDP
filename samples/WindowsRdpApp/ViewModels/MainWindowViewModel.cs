using System;
using System.Windows.Input;
using ReactiveUI;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;

namespace WindowsRdpApp.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private object? _currentView;
    private object? _selectedNavItem;
    private string _statusMessage = "Ready — CDP Server active on port 9225";
    private int _cdpPort = 9225;

    public QuickConnectViewModel QuickConnectVM { get; }
    public ProfilesViewModel ProfilesVM { get; }
    public SessionWorkspaceViewModel SessionWorkspaceVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public object? CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public object? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNavItem, value);
            OnNavItemChanged(value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public int CdpPort
    {
        get => _cdpPort;
        set => this.RaiseAndSetIfChanged(ref _cdpPort, value);
    }

    public ICommand NavigateQuickConnectCommand { get; }
    public ICommand NavigateProfilesCommand { get; }
    public ICommand NavigateWorkspaceCommand { get; }
    public ICommand NavigateSettingsCommand { get; }

    public MainWindowViewModel(IProfileStorageService? storageService = null)
    {
        QuickConnectVM = new QuickConnectViewModel();
        ProfilesVM = new ProfilesViewModel(storageService);
        SessionWorkspaceVM = new SessionWorkspaceViewModel();
        SettingsVM = new SettingsViewModel();

        NavigateQuickConnectCommand = ReactiveCommand.Create(() => CurrentView = QuickConnectVM);
        NavigateProfilesCommand = ReactiveCommand.Create(() => CurrentView = ProfilesVM);
        NavigateWorkspaceCommand = ReactiveCommand.Create(() => CurrentView = SessionWorkspaceVM);
        NavigateSettingsCommand = ReactiveCommand.Create(() => CurrentView = SettingsVM);

        // Wire quick connect & profiles request events to open session in workspace
        QuickConnectVM.RequestConnect += OnConnectRequested;
        ProfilesVM.RequestConnect += OnConnectRequested;

        // Default view
        CurrentView = QuickConnectVM;
    }

    private void OnConnectRequested(RdpConnectionProfile profile)
    {
        SessionWorkspaceVM.OpenSession(profile);
        CurrentView = SessionWorkspaceVM;
        StatusMessage = $"Active Session: {profile.Name} ({profile.Host}:{profile.Port})";
    }

    private void OnNavItemChanged(object? item)
    {
        if (item == null) return;

        string tagOrHeader = item.ToString() ?? string.Empty;
        if (item is CDP.FluentNavigation.NavigationViewItem navItem)
        {
            tagOrHeader = navItem.Tag?.ToString() ?? navItem.Content?.ToString() ?? string.Empty;
        }

        if (tagOrHeader.Equals("QuickConnect", StringComparison.OrdinalIgnoreCase) ||
            tagOrHeader.Contains("Quick", StringComparison.OrdinalIgnoreCase))
        {
            CurrentView = QuickConnectVM;
        }
        else if (tagOrHeader.Equals("Profiles", StringComparison.OrdinalIgnoreCase) ||
                 tagOrHeader.Contains("Profile", StringComparison.OrdinalIgnoreCase))
        {
            CurrentView = ProfilesVM;
        }
        else if (tagOrHeader.Equals("Workspace", StringComparison.OrdinalIgnoreCase) ||
                 tagOrHeader.Contains("Workspace", StringComparison.OrdinalIgnoreCase))
        {
            CurrentView = SessionWorkspaceVM;
        }
        else if (tagOrHeader.Equals("Settings", StringComparison.OrdinalIgnoreCase) ||
                 tagOrHeader.Contains("Setting", StringComparison.OrdinalIgnoreCase))
        {
            CurrentView = SettingsVM;
        }
    }
}
