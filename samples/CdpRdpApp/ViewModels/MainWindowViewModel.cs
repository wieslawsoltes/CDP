using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CDP.Rdp.Session;
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

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly Func<RdpSessionOptions, IRdpSession> _sessionFactory;
    public ConnectionStateViewModel Connection { get; } = new();
    public RecorderStateViewModel Recorder { get; } = new();
    private IRdpSession? _session;

    public IRdpSession? Session
    {
        get => _session;
        private set => this.RaiseAndSetIfChanged(ref _session, value);
    }

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

    public MainWindowViewModel(Func<RdpSessionOptions, IRdpSession>? sessionFactory = null)
    {
        _sessionFactory = sessionFactory ?? (options => new RdpClient(options));
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
        RefreshTargetsCommand = ReactiveCommand.Create(ExecuteRefreshTargets);
        ToggleRecordCommand = ReactiveCommand.Create(ExecuteToggleRecord);
    }

    public async Task ConnectAsync()
    {
        if (Port is < 1 or > 65535)
        {
            Connection.StatusText = "Port must be in the range 1-65535";
            return;
        }

        await DisconnectSessionAsync();
        var options = new RdpSessionOptions
        {
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password
        };
        IRdpSession client = _sessionFactory(options);
        client.StateChanged += OnSessionStateChanged;
        Session = client;

        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            Connection.IsConnected = false;
            Connection.StatusText = $"Connection failed: {ex.Message}";
        }
    }

    public async Task DisconnectAsync()
    {
        await DisconnectSessionAsync();
    }

    private void ExecuteRefreshTargets()
    {
    }

    private void ExecuteToggleRecord()
    {
        Recorder.IsRecording = !Recorder.IsRecording;
    }

    private void OnSessionStateChanged(object? sender, RdpConnectionStateChangedEventArgs e)
    {
        void ApplyState()
        {
            Connection.IsConnected = e.NewState == RdpConnectionState.Connected;
            Connection.StatusText = e.Exception == null
                ? e.NewState.ToString()
                : $"{e.NewState}: {e.Exception.Message}";
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyState();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyState);
        }
    }

    private async Task DisconnectSessionAsync()
    {
        IRdpSession? session = Session;
        Session = null;
        if (session != null)
        {
            session.StateChanged -= OnSessionStateChanged;
            try
            {
                await session.DisconnectAsync();
            }
            finally
            {
                await session.DisposeAsync();
            }
        }

        Connection.IsConnected = false;
        Connection.StatusText = "Disconnected";
    }

    public void Dispose()
    {
        DisconnectSessionAsync().GetAwaiter().GetResult();
    }
}
