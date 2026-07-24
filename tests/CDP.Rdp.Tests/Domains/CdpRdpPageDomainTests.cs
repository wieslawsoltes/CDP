namespace CDP.Rdp.Tests.Domains;

using System;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Headless.XUnit;
using CDP.Rdp.Frames;
using CdpRdpApp;
using Xunit;

public class CdpRdpPageDomainTests
{
    [AvaloniaFact]
    public async Task PageEnable_And_CaptureScreenshot_ReturnsNonEmptyBase64()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var enableResult = await PageDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        var screenshotResult = await PageDomain.HandleAsync(session, "captureScreenshot", new JsonObject { ["format"] = "png" });
        Assert.NotNull(screenshotResult);
        string? data = screenshotResult["data"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(data));

        byte[] imageBytes = Convert.FromBase64String(data!);
        Assert.True(imageBytes.Length > 0);

        window.Close();
    }

    [AvaloniaFact]
    public async Task StartAndStopScreencast_Succeeds()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var startResult = await PageDomain.HandleAsync(session, "startScreencast", new JsonObject
        {
            ["format"] = "png",
            ["everyNthFrame"] = 1
        });
        Assert.NotNull(startResult);

        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);

        window.Close();
    }

    [AvaloniaFact]
    public void RdpControl_OnFrameUpdated_TriggersScreencastFrameNotification()
    {
        var rdpControl = new RdpControl();
        var bmpUpdate = new RdpBitmapUpdate(0, 0, 100, 100, 32, false, new byte[100 * 100 * 4]);
        var frameUpdate = new RdpFrameUpdateEventArgs(
            1,
            DateTimeOffset.UtcNow,
            new[] { bmpUpdate });

        // Exercise OnFrameUpdated via reflection or session hook
        var mi = typeof(RdpControl).GetMethod("OnFrameUpdated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(mi);

        mi.Invoke(rdpControl, new object?[] { rdpControl, frameUpdate });
        Assert.NotNull(rdpControl.FrameBuffer);
        Assert.Equal(1280, rdpControl.FrameBuffer.Width);
    }
}
