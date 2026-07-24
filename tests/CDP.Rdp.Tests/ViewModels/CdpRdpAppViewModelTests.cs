namespace CDP.Rdp.Tests.ViewModels;

using CdpRdpApp.ViewModels;
using Xunit;

public class CdpRdpAppViewModelTests
{
    [Fact]
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

    [Fact]
    public void MainWindowViewModel_ConnectCommand_UpdatesConnectionState()
    {
        var vm = new MainWindowViewModel();

        vm.ConnectCommand.Execute(null);

        Assert.True(vm.Connection.IsConnected);
        Assert.Equal("Connected", vm.Connection.StatusText);
    }

    [Fact]
    public void MainWindowViewModel_DisconnectCommand_UpdatesConnectionState()
    {
        var vm = new MainWindowViewModel();
        vm.ConnectCommand.Execute(null);

        vm.DisconnectCommand.Execute(null);

        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);
    }

    [Fact]
    public void MainWindowViewModel_ToggleRecordCommand_TogglesRecording()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.True(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.False(vm.Recorder.IsRecording);
    }

    [Fact]
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
}
