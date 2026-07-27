using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using ReactiveUI;
using WindowsRdpApp.Models;

namespace WindowsRdpApp.ViewModels;

public class SessionWorkspaceViewModel : ReactiveObject
{
    private RdpSessionTab? _selectedSession;
    private string _statusText = "Workspace ready";
    private bool _isFullScreen;

    public ObservableCollection<RdpSessionTab> Sessions { get; } = new();

    public RdpSessionTab? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_selectedSession != value)
            {
                if (_selectedSession != null) _selectedSession.IsActive = false;
                this.RaiseAndSetIfChanged(ref _selectedSession, value);
                if (_selectedSession != null) _selectedSession.IsActive = true;
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set => this.RaiseAndSetIfChanged(ref _isFullScreen, value);
    }

    public ICommand CloseSessionCommand { get; }
    public ICommand DisconnectAllCommand { get; }
    public ICommand NewSessionCommand { get; }
    public ICommand ToggleFullScreenCommand { get; }
    public ICommand SendKeyComboCommand { get; }

    public SessionWorkspaceViewModel()
    {
        CloseSessionCommand = ReactiveCommand.CreateFromTask<RdpSessionTab>(ExecuteCloseSessionAsync);
        DisconnectAllCommand = ReactiveCommand.CreateFromTask(ExecuteDisconnectAllAsync);
        NewSessionCommand = ReactiveCommand.Create(ExecuteNewSession);
        ToggleFullScreenCommand = ReactiveCommand.Create(() => IsFullScreen = !IsFullScreen);
        SendKeyComboCommand = ReactiveCommand.CreateFromTask<string>(ExecuteSendKeyComboAsync);

        // Add an initial workspace tab if empty
        OpenSession(new RdpConnectionProfile
        {
            Name = "Local Desktop",
            Host = "127.0.0.1",
            Port = 3389,
            Username = "admin"
        });
    }

    public RdpSessionTab OpenSession(
        RdpConnectionProfile profile,
        Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>>? customTransportFactory = null)
    {
        var tab = new RdpSessionTab
        {
            Title = string.IsNullOrWhiteSpace(profile.Name) ? "RDP Session" : profile.Name,
            Host = string.IsNullOrWhiteSpace(profile.Host) ? "127.0.0.1" : profile.Host,
            Port = profile.Port <= 0 ? 3389 : profile.Port,
            Username = profile.Username,
            Password = profile.Password,
            Domain = profile.Domain,
            Width = profile.Width <= 0 ? 1920 : profile.Width,
            Height = profile.Height <= 0 ? 1080 : profile.Height,
            ColorDepth = profile.ColorDepth <= 0 ? 32 : profile.ColorDepth,
            Status = "Connected",
            IsActive = true
        };

        Sessions.Add(tab);
        SelectedSession = tab;
        StatusText = $"Connected to {profile.Name} ({profile.Host}:{profile.Port})";

        // Connect live session in background
        _ = tab.ConnectSessionAsync(customTransportFactory);

        return tab;
    }

    public async Task ExecuteCloseSessionAsync(RdpSessionTab? tab)
    {
        var target = tab ?? SelectedSession;
        if (target != null)
        {
            await target.DisconnectSessionAsync();
            target.Dispose();
            if (SelectedSession == target)
            {
                var remaining = Sessions.Where(s => s != target).ToList();
                SelectedSession = remaining.LastOrDefault();
            }
            Sessions.Remove(target);
            StatusText = $"Closed session '{target.Title}'";
        }
    }

    public async Task ExecuteDisconnectAllAsync()
    {
        int count = Sessions.Count;
        var copy = Sessions.ToList();

        SelectedSession = null;
        Sessions.Clear();

        foreach (var session in copy)
        {
            await session.DisconnectSessionAsync();
            session.Dispose();
        }
        StatusText = $"Disconnected all {count} sessions";
    }

    private void ExecuteNewSession()
    {
        OpenSession(new RdpConnectionProfile
        {
            Name = $"Session {Sessions.Count + 1}",
            Host = "127.0.0.1",
            Port = 3389,
            Username = "admin"
        });
    }

    private async Task ExecuteSendKeyComboAsync(string? comboName)
    {
        if (SelectedSession == null || string.IsNullOrEmpty(comboName)) return;

        if (Enum.TryParse<RdpKeyCombination>(comboName, ignoreCase: true, out var combo))
        {
            await SelectedSession.SendKeyPassthroughAsync(combo);
            StatusText = $"Sent {comboName} to {SelectedSession.Title}";
        }
    }
}
