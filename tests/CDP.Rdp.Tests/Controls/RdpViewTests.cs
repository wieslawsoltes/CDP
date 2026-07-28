namespace CDP.Rdp.Tests.Controls;

using Avalonia.Diagnostics.Cdp.Rdp;
using Xunit;

public class RdpViewTests
{
    [AvaloniaFact]
    public void RdpView_Defaults_InitializedCorrectly()
    {
        var view = new RdpView();

        Assert.Equal("127.0.0.1", view.Host);
        Assert.Equal(3389, view.Port);
        Assert.Equal(string.Empty, view.Username);
        Assert.Equal(string.Empty, view.Password);
        Assert.Equal(string.Empty, view.Domain);
        Assert.False(view.IsConnected);
        Assert.Null(view.Session);
        Assert.Null(view.ConnectCommand);
        Assert.Null(view.DisconnectCommand);
    }

    [AvaloniaFact]
    public void RdpView_StyledProperties_CanBeSetAndRetrieved()
    {
        var view = new RdpView
        {
            Host = "10.0.0.5",
            Port = 3389,
            Username = "user",
            Password = "pass",
            Domain = "WORKGROUP",
            IsConnected = true
        };

        Assert.Equal("10.0.0.5", view.Host);
        Assert.Equal(3389, view.Port);
        Assert.Equal("user", view.Username);
        Assert.Equal("pass", view.Password);
        Assert.Equal("WORKGROUP", view.Domain);
        Assert.True(view.IsConnected);
    }
}
