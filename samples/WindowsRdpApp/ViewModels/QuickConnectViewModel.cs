using System;
using System.Windows.Input;
using ReactiveUI;
using WindowsRdpApp.Models;

namespace WindowsRdpApp.ViewModels;

public class QuickConnectViewModel : ReactiveObject
{
    private string _host = "127.0.0.1";
    private int _port = 3389;
    private string _username = "admin";
    private string _password = string.Empty;
    private string _domain = string.Empty;
    private bool _saveAsProfile = true;
    private string _profileName = "Quick Connection";
    private string _statusText = "Ready to connect";

    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string Domain
    {
        get => _domain;
        set => this.RaiseAndSetIfChanged(ref _domain, value);
    }

    public bool SaveAsProfile
    {
        get => _saveAsProfile;
        set => this.RaiseAndSetIfChanged(ref _saveAsProfile, value);
    }

    public string ProfileName
    {
        get => _profileName;
        set => this.RaiseAndSetIfChanged(ref _profileName, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ICommand ConnectCommand { get; }
    public ICommand ClearCommand { get; }

    public event Action<RdpConnectionProfile>? RequestConnect;

    public QuickConnectViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(ExecuteConnect);
        ClearCommand = ReactiveCommand.Create(ExecuteClear);
    }

    private void ExecuteConnect()
    {
        var profile = new RdpConnectionProfile
        {
            Name = string.IsNullOrWhiteSpace(ProfileName) ? $"RDP-{Host}:{Port}" : ProfileName,
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            Domain = Domain,
            LastConnected = DateTime.UtcNow
        };

        StatusText = $"Connecting to {Host}:{Port}...";
        RequestConnect?.Invoke(profile);
    }

    private void ExecuteClear()
    {
        Host = "127.0.0.1";
        Port = 3389;
        Username = "admin";
        Password = string.Empty;
        Domain = string.Empty;
        ProfileName = "Quick Connection";
        StatusText = "Cleared fields";
    }
}
