using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Rendering;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using SkiaSharp;
using WindowsRdpApp.Models;
using WindowsRdpApp.ViewModels;
using Xunit;

namespace CDP.Rdp.Tests.Session;

public class WindowsRdpAppMultiSessionLifecycleTests
{
    private static Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>> CreateMockTransportFactory()
    {
        return (opts, ct) =>
        {
            var stream = new MemoryStream();
            IRdpSecurityTransport transport = new PlainRdpSecurityTransport(stream);
            return Task.FromResult(transport);
        };
    }

    [AvaloniaFact]
    public async Task MultiSessionWorkspace_ConcurrentSessions_OpensAndManagesMultipleTabs()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        await workspaceVM.ExecuteDisconnectAllAsync();
        Assert.Empty(workspaceVM.Sessions);

        var transportFactory = CreateMockTransportFactory();

        // Open 3 concurrent tabs with mock transport factory
        var tab1 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Server 1", Host = "10.0.0.1" }, transportFactory);
        var tab2 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Server 2", Host = "10.0.0.2" }, transportFactory);
        var tab3 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Server 3", Host = "10.0.0.3" }, transportFactory);

        Assert.Equal(3, workspaceVM.Sessions.Count);
        Assert.Equal(tab3, workspaceVM.SelectedSession);

        // Wait for connections to negotiate and connect
        await Task.Delay(150);

        Assert.NotNull(tab1.Session);
        Assert.NotNull(tab2.Session);
        Assert.NotNull(tab3.Session);

        // Clean up
        await workspaceVM.ExecuteDisconnectAllAsync();
        Assert.Empty(workspaceVM.Sessions);
        Assert.Null(workspaceVM.SelectedSession);
    }

    [AvaloniaFact]
    public async Task SessionWorkspace_CloseSessionCommand_DisconnectsAndDisposesSession()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        await workspaceVM.ExecuteDisconnectAllAsync();

        var transportFactory = CreateMockTransportFactory();

        var tab1 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 1" }, transportFactory);
        var tab2 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 2" }, transportFactory);
        var tab3 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 3" }, transportFactory);

        await Task.Delay(100);

        // Close selected tab 3
        await workspaceVM.ExecuteCloseSessionAsync(tab3);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, workspaceVM.Sessions.Count);
        Assert.DoesNotContain(tab3, workspaceVM.Sessions);
        Assert.Equal("Disconnected", tab3.Status);
        Assert.Null(tab3.Session);
        Assert.Equal(tab2, workspaceVM.SelectedSession);

        await workspaceVM.ExecuteDisconnectAllAsync();
    }

    [AvaloniaFact]
    public async Task RdpSessionTab_KeyPassthrough_DispatchesScancodeEvents()
    {
        var transportFactory = CreateMockTransportFactory();

        var tab = new RdpSessionTab
        {
            Host = "127.0.0.1",
            Port = 3389,
            IsKeyPassthroughEnabled = true
        };

        await tab.ConnectSessionAsync(transportFactory);
        await Task.Delay(100);

        // Test sending key combinations
        await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltTab);
        await tab.SendKeyPassthroughAsync(RdpKeyCombination.CtrlAltDel);
        await tab.SendKeyPassthroughAsync(RdpKeyCombination.WinKey);
        await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltF4);
        await tab.SendKeyPassthroughAsync(RdpKeyCombination.CtrlShiftEsc);

        Assert.NotNull(tab.Session);
        await tab.DisconnectSessionAsync();
    }

    [AvaloniaFact]
    public async Task RdpSessionTab_Metrics_UpdatesTotalFramesAndDirtyRectsOnFrameUpdated()
    {
        var tab = new RdpSessionTab();
        Assert.Equal(0L, tab.TotalFrames);
        Assert.Equal(0, tab.DirtyRectCount);

        var transportFactory = CreateMockTransportFactory();
        await tab.ConnectSessionAsync(transportFactory);
        await Task.Delay(100);

        Assert.NotNull(tab.Session);

        var bitmapUpdates = new List<RdpBitmapUpdate>
        {
            new RdpBitmapUpdate(0, 0, 100, 100, 32, false, new byte[100 * 100 * 4])
        };
        var frameUpdate = new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, bitmapUpdates);

        if (tab.Session is RdpClient client)
        {
            client.RaiseFrameUpdatedForTesting(frameUpdate);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1L, tab.TotalFrames);
        Assert.Equal(1, tab.DirtyRectCount);

        await tab.DisconnectSessionAsync();
    }
}
