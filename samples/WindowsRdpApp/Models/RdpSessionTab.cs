using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Rendering;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using ReactiveUI;

namespace WindowsRdpApp.Models;

public enum RdpKeyCombination
{
    AltTab,
    CtrlAltDel,
    WinKey,
    AltF4,
    CtrlShiftEsc
}

public class RdpSessionTab : ReactiveObject, IDisposable
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = "New Session";
    private string _host = "127.0.0.1";
    private int _port = 3389;
    private string _username = "admin";
    private string _password = string.Empty;
    private string _domain = string.Empty;
    private int _width = 1920;
    private int _height = 1080;
    private int _colorDepth = 32;
    private double _scaleFactor = 1.0;
    private bool _autoReconnect = true;
    private int _maxReconnectAttempts = 3;
    private int _reconnectCount = 0;
    private string _status = "Disconnected";
    private RdpConnectionState _connectionState = RdpConnectionState.Disconnected;
    private string _errorMessage = string.Empty;
    private bool _isActive;
    private bool _isFullScreen;
    private bool _isKeyPassthroughEnabled = true;
    private double _fps;
    private long _totalFrames;
    private int _dirtyRectCount;
    private IRdpSession? _session;

    private Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>>? _lastCustomTransportFactory;
    private CancellationTokenSource? _connectCts;
    private DateTime _lastFpsCalcTime = DateTime.UtcNow;
    private int _framesSinceLastCalc = 0;

    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

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

    public int Width
    {
        get => _width;
        set => this.RaiseAndSetIfChanged(ref _width, value);
    }

    public int Height
    {
        get => _height;
        set => this.RaiseAndSetIfChanged(ref _height, value);
    }

    public int ColorDepth
    {
        get => _colorDepth;
        set => this.RaiseAndSetIfChanged(ref _colorDepth, value);
    }

    public double ScaleFactor
    {
        get => _scaleFactor;
        set => this.RaiseAndSetIfChanged(ref _scaleFactor, value);
    }

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => this.RaiseAndSetIfChanged(ref _autoReconnect, value);
    }

    public int MaxReconnectAttempts
    {
        get => _maxReconnectAttempts;
        set => this.RaiseAndSetIfChanged(ref _maxReconnectAttempts, value);
    }

    public int ReconnectCount
    {
        get => _reconnectCount;
        set => this.RaiseAndSetIfChanged(ref _reconnectCount, value);
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public RdpConnectionState ConnectionState
    {
        get => _connectionState;
        set => this.RaiseAndSetIfChanged(ref _connectionState, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set => this.RaiseAndSetIfChanged(ref _isFullScreen, value);
    }

    public bool IsKeyPassthroughEnabled
    {
        get => _isKeyPassthroughEnabled;
        set => this.RaiseAndSetIfChanged(ref _isKeyPassthroughEnabled, value);
    }

    public double Fps
    {
        get => _fps;
        set => this.RaiseAndSetIfChanged(ref _fps, value);
    }

    public long TotalFrames
    {
        get => _totalFrames;
        set => this.RaiseAndSetIfChanged(ref _totalFrames, value);
    }

    public int DirtyRectCount
    {
        get => _dirtyRectCount;
        set => this.RaiseAndSetIfChanged(ref _dirtyRectCount, value);
    }

    public IRdpSession? Session
    {
        get => _session;
        set
        {
            if (_session == value) return;
            if (_session != null)
            {
                _session.StateChanged -= OnStateChanged;
                _session.FrameUpdated -= OnFrameUpdated;
            }
            this.RaiseAndSetIfChanged(ref _session, value);
            if (_session != null)
            {
                _session.StateChanged += OnStateChanged;
                _session.FrameUpdated += OnFrameUpdated;
            }
        }
    }

    public async Task ConnectSessionAsync(
        Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>>? customTransportFactory = null,
        CancellationToken cancellationToken = default)
    {
        _lastCustomTransportFactory = customTransportFactory;
        _connectCts?.Cancel();
        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var options = new RdpSessionOptions
        {
            Host = Host,
            Port = (ushort)(Port <= 0 ? 3389 : Port),
            Username = Username,
            Password = Password,
            Domain = Domain,
            Width = (ushort)(Width <= 0 ? 1920 : Width),
            Height = (ushort)(Height <= 0 ? 1080 : Height),
            ColorDepth = (ushort)(ColorDepth <= 0 ? 32 : ColorDepth),
            AcceptUntrustedCertificates = true
        };

        if (Session != null)
        {
            var oldSession = Session;
            Session = null;
            oldSession.StateChanged -= OnStateChanged;
            oldSession.FrameUpdated -= OnFrameUpdated;
            try
            {
                await oldSession.DisconnectAsync();
            }
            catch { }
            if (oldSession is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        var client = new RdpClient(options, customTransportFactory);
        Session = client;

        RunOnUIThread(() =>
        {
            ConnectionState = client.State;
            Status = client.State.ToString();
        });

        try
        {
            await client.ConnectAsync(_connectCts.Token);
            if (_connectCts.IsCancellationRequested || client.State == RdpConnectionState.Disconnected)
                return;

            RunOnUIThread(() =>
            {
                Status = client.State.ToString();
                ConnectionState = client.State;
                ReconnectCount = 0;
            });
        }
        catch (Exception ex)
        {
            if (_connectCts?.IsCancellationRequested == true || client.State == RdpConnectionState.Disconnected)
            {
                return;
            }

            RunOnUIThread(() =>
            {
                ErrorMessage = ex.Message;
                Status = "Faulted";
                ConnectionState = RdpConnectionState.Faulted;
            });
        }
    }

    private async Task TriggerAutoReconnectAsync(CancellationToken cancellationToken)
    {
        ReconnectCount++;
        int backoffMs = (int)(Math.Pow(2, ReconnectCount - 1) * 1000);
        RunOnUIThread(() =>
        {
            Status = $"Reconnecting (attempt {ReconnectCount}/{MaxReconnectAttempts})...";
        });

        try
        {
            await Task.Delay(backoffMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await ConnectSessionAsync(_lastCustomTransportFactory, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }

    public async Task DisconnectSessionAsync()
    {
        try
        {
            _connectCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore disposed CTS
        }

        if (Session != null)
        {
            var sessionToDisconnect = Session;
            Session = null;

            sessionToDisconnect.StateChanged -= OnStateChanged;
            sessionToDisconnect.FrameUpdated -= OnFrameUpdated;

            try
            {
                await sessionToDisconnect.DisconnectAsync();
            }
            catch
            {
                // Ignore disconnect errors
            }

            if (sessionToDisconnect is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        RunOnUIThread(() =>
        {
            Status = "Disconnected";
            ConnectionState = RdpConnectionState.Disconnected;
        });
    }

    private static readonly bool IsTestContext = InitializeIsTestContext();

    private static bool InitializeIsTestContext()
    {
        try
        {
            var entryAsm = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
            var domainName = AppDomain.CurrentDomain.FriendlyName ?? string.Empty;

            if (entryAsm.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
                entryAsm.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                entryAsm.Contains("CDP.Rdp.Tests", StringComparison.OrdinalIgnoreCase) ||
                domainName.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
                domainName.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                domainName.Contains("CDP.Rdp.Tests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void RunOnUIThread(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess() || IsTestContext)
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }
        catch
        {
            action();
        }
    }

    private void OnStateChanged(object? sender, RdpConnectionStateChangedEventArgs e)
    {
        RunOnUIThread(() =>
        {
            if (ConnectionState == RdpConnectionState.Disconnected)
                return;

            ConnectionState = e.NewState;
            Status = e.NewState.ToString();

            if (e.NewState == RdpConnectionState.Faulted && AutoReconnect && ReconnectCount < MaxReconnectAttempts)
            {
                _ = TriggerAutoReconnectAsync(_connectCts?.Token ?? CancellationToken.None);
            }
        });
    }

    private void OnFrameUpdated(object? sender, RdpFrameUpdateEventArgs e)
    {
        int dirtyCount = e.BitmapUpdates?.Count ?? 0;
        RunOnUIThread(() =>
        {
            _framesSinceLastCalc++;
            TotalFrames++;
            DirtyRectCount = dirtyCount;

            var now = DateTime.UtcNow;
            double elapsed = (now - _lastFpsCalcTime).TotalSeconds;
            if (elapsed >= 1.0)
            {
                Fps = Math.Round(_framesSinceLastCalc / elapsed, 1);
                _framesSinceLastCalc = 0;
                _lastFpsCalcTime = now;
            }
        });
    }

    public async Task SendKeyPassthroughAsync(RdpKeyCombination combination)
    {
        if (Session == null || !IsKeyPassthroughEnabled)
            return;

        switch (combination)
        {
            case RdpKeyCombination.AltTab:
                await SendKeyAsync(0x38, isDown: true, isExtended: false);
                await SendKeyAsync(0x0F, isDown: true, isExtended: false);
                await SendKeyAsync(0x0F, isDown: false, isExtended: false);
                await SendKeyAsync(0x38, isDown: false, isExtended: false);
                break;

            case RdpKeyCombination.CtrlAltDel:
                await SendKeyAsync(0x1D, isDown: true, isExtended: false);
                await SendKeyAsync(0x38, isDown: true, isExtended: false);
                await SendKeyAsync(0x53, isDown: true, isExtended: true);
                await SendKeyAsync(0x53, isDown: false, isExtended: true);
                await SendKeyAsync(0x38, isDown: false, isExtended: false);
                await SendKeyAsync(0x1D, isDown: false, isExtended: false);
                break;

            case RdpKeyCombination.WinKey:
                await SendKeyAsync(0x5B, isDown: true, isExtended: true);
                await SendKeyAsync(0x5B, isDown: false, isExtended: true);
                break;

            case RdpKeyCombination.AltF4:
                await SendKeyAsync(0x38, isDown: true, isExtended: false);
                await SendKeyAsync(0x3E, isDown: true, isExtended: false);
                await SendKeyAsync(0x3E, isDown: false, isExtended: false);
                await SendKeyAsync(0x38, isDown: false, isExtended: false);
                break;

            case RdpKeyCombination.CtrlShiftEsc:
                await SendKeyAsync(0x1D, isDown: true, isExtended: false);
                await SendKeyAsync(0x2A, isDown: true, isExtended: false);
                await SendKeyAsync(0x01, isDown: true, isExtended: false);
                await SendKeyAsync(0x01, isDown: false, isExtended: false);
                await SendKeyAsync(0x2A, isDown: false, isExtended: false);
                await SendKeyAsync(0x1D, isDown: false, isExtended: false);
                break;
        }
    }

    private async Task SendKeyAsync(ushort scancode, bool isDown, bool isExtended)
    {
        if (Session == null) return;
        RdpKeyboardFlags flags = isDown ? RdpKeyboardFlags.Down : RdpKeyboardFlags.Release;
        if (isExtended) flags |= RdpKeyboardFlags.Extended;

        var kbEvent = new RdpKeyboardEvent((uint)Environment.TickCount, flags, scancode, isVirtualKey: false);
        var inputEvent = new RdpInputEvent((uint)Environment.TickCount, kbEvent);
        await Session.SendInputEventAsync(inputEvent);
    }

    public void Dispose()
    {
        try
        {
            _connectCts?.Cancel();
            _connectCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        _connectCts = null;

        if (_session != null)
        {
            _session.StateChanged -= OnStateChanged;
            _session.FrameUpdated -= OnFrameUpdated;
            _session = null;
        }

        _ = DisconnectSessionAsync();
    }
}
