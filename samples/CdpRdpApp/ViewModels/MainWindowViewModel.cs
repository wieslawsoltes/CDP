using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;

namespace CdpRdpApp.ViewModels;

public class ConnectionStateViewModel : ReactiveObject
{
    private string _host = "127.0.0.1";
    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    private int _port = 3389;
    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private string _username = "admin";
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private string _statusText = "Disconnected";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public void ResetForm()
    {
        Host = "127.0.0.1";
        Port = 3389;
        Username = "admin";
        Password = "";
        IsConnected = false;
        StatusText = "Disconnected";
    }
}

public class RecorderStateViewModel : ReactiveObject
{
    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    public ObservableCollection<string> RecordedSteps { get; } = new();
    public object TestStudio { get; } = new();
}

public class MainWindowViewModel : ReactiveObject
{
    public ConnectionStateViewModel Connection { get; } = new();
    public RecorderStateViewModel Recorder { get; } = new();

    public string Host
    {
        get => Connection.Host;
        set => Connection.Host = value;
    }

    public int Port
    {
        get => Connection.Port;
        set => Connection.Port = value;
    }

    public string Username
    {
        get => Connection.Username;
        set => Connection.Username = value;
    }

    public string Password
    {
        get => Connection.Password;
        set => Connection.Password = value;
    }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand RefreshTargetsCommand { get; }
    public ICommand ToggleRecordCommand { get; }

    public MainWindowViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(ExecuteConnect);
        DisconnectCommand = ReactiveCommand.Create(ExecuteDisconnect);
        RefreshTargetsCommand = ReactiveCommand.Create(ExecuteRefreshTargets);
        ToggleRecordCommand = ReactiveCommand.Create(ExecuteToggleRecord);
    }

    private void ExecuteConnect()
    {
        Connection.IsConnected = true;
        Connection.StatusText = "Connected";
    }

    private void ExecuteDisconnect()
    {
        Connection.IsConnected = false;
        Connection.StatusText = "Disconnected";
    }

    private void ExecuteRefreshTargets()
    {
    }

    private void ExecuteToggleRecord()
    {
        Recorder.IsRecording = !Recorder.IsRecording;
    }
}
