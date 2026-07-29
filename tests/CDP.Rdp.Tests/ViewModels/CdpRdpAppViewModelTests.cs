namespace CDP.Rdp.Tests.ViewModels;

using CdpRdpApp.ViewModels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Session;
using Xunit;

[Xunit.Collection("RdpTests")]
public class CdpRdpAppViewModelTests
{
    [AvaloniaFact]
    public void MainWindowViewModel_InitialState_Disconnected()
    {
        var vm = new MainWindowViewModel();

        Assert.Equal("127.0.0.1", vm.Host);
        Assert.Equal(3389, vm.Port);
        Assert.Equal("admin", vm.Username);
        Assert.Equal(string.Empty, vm.Password);
        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);
        Assert.False(vm.Recorder.IsRecording);
    }

    [AvaloniaFact]
    public async Task MainWindowViewModel_ConnectAsync_UsesSessionState()
    {
        using var session = new TestSession();
        using var vm = new MainWindowViewModel(_ => session);

        await vm.ConnectAsync();

        Assert.True(vm.Connection.IsConnected);
        Assert.Equal("Connected", vm.Connection.StatusText);
        Assert.Same(session, vm.Session);
    }

    [AvaloniaFact]
    public async Task MainWindowViewModel_DisconnectAsync_DisposesSession()
    {
        using var session = new TestSession();
        using var vm = new MainWindowViewModel(_ => session);
        await vm.ConnectAsync();

        await vm.DisconnectAsync();

        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);
        Assert.True(session.IsDisposed);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_ToggleRecordCommand_TogglesRecording()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.True(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.False(vm.Recorder.IsRecording);
    }

    [AvaloniaFact]
    public void ConnectionStateViewModel_ResetForm_ResetsValues()
    {
        var conn = new ConnectionStateViewModel
        {
            Host = "192.168.1.50",
            Port = 9999,
            Username = "testuser",
            Password = "password",
            IsConnected = true,
            StatusText = "Connected"
        };

        conn.ResetForm();

        Assert.Equal("127.0.0.1", conn.Host);
        Assert.Equal(3389, conn.Port);
        Assert.Equal("admin", conn.Username);
        Assert.Equal(string.Empty, conn.Password);
        Assert.False(conn.IsConnected);
        Assert.Equal("Disconnected", conn.StatusText);
    }

    private sealed class TestSession : IRdpSession
    {
        public RdpConnectionState State { get; private set; } = RdpConnectionState.Disconnected;
        public RdpSessionOptions Options { get; } = new();
        public bool IsDisposed { get; private set; }

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(RdpConnectionState.Connected);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(RdpConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private void SetState(RdpConnectionState state)
        {
            RdpConnectionState oldState = State;
            State = state;
            StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(oldState, state));
        }
    }
}
