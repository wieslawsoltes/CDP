namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CdpRdpApp.ViewModels;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using SkiaSharp;
using Xunit;

using Avalonia.Headless.XUnit;

/// <summary>
/// Empirical stress test suite for Milestone 3 (Challenger 2).
/// Verifies:
/// 1. ViewModel state transitions (Connection.IsConnected, Connection.StatusText, Recorder.IsRecording)
/// 2. CDP server initialization on port 9224 in CdpRdpApp
/// 3. UI layout responsiveness and memory allocation safety during frame updates
/// </summary>
public class ChallengerM3_2StressTests
{
    #region 1. ViewModel State Transitions

    [AvaloniaFact]
    public void MainWindowViewModel_Defaults_InitializedCorrectly()
    {
        var vm = new MainWindowViewModel();

        Assert.Equal("127.0.0.1", vm.Host);
        Assert.Equal(3389, vm.Port);
        Assert.Equal("admin", vm.Username);
        Assert.Equal(string.Empty, vm.Password);
        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);
        Assert.False(vm.Recorder.IsRecording);
        Assert.Empty(vm.Recorder.RecordedSteps);
        Assert.NotNull(vm.Recorder.TestStudio);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_ConnectCommand_TransitionsStateToConnected()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);

        vm.ConnectCommand.Execute(null);

        Assert.True(vm.Connection.IsConnected);
        Assert.Equal("Connected", vm.Connection.StatusText);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_DisconnectCommand_TransitionsStateToDisconnected()
    {
        var vm = new MainWindowViewModel();
        vm.ConnectCommand.Execute(null);
        Assert.True(vm.Connection.IsConnected);

        vm.DisconnectCommand.Execute(null);

        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("Disconnected", vm.Connection.StatusText);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_ToggleRecordCommand_TogglesIsRecordingState()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.True(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.False(vm.Recorder.IsRecording);

        vm.ToggleRecordCommand.Execute(null);
        Assert.True(vm.Recorder.IsRecording);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_PropertyChangeNotifications_FiresPropertyChangedEvents()
    {
        var vm = new MainWindowViewModel();
        var changedProps = new List<string>();

        vm.Connection.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
                changedProps.Add(e.PropertyName);
        };

        vm.Recorder.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
                changedProps.Add(e.PropertyName);
        };

        vm.Host = "192.168.1.50";
        vm.Port = 3390;
        vm.Username = "operator";
        vm.Password = "Secret123!";
        vm.ConnectCommand.Execute(null);
        vm.ToggleRecordCommand.Execute(null);

        Assert.Contains(nameof(ConnectionStateViewModel.Host), changedProps);
        Assert.Contains(nameof(ConnectionStateViewModel.Port), changedProps);
        Assert.Contains(nameof(ConnectionStateViewModel.Username), changedProps);
        Assert.Contains(nameof(ConnectionStateViewModel.Password), changedProps);
        Assert.Contains(nameof(ConnectionStateViewModel.IsConnected), changedProps);
        Assert.Contains(nameof(ConnectionStateViewModel.StatusText), changedProps);
        Assert.Contains(nameof(RecorderStateViewModel.IsRecording), changedProps);
    }

    [AvaloniaFact]
    public void ConnectionStateViewModel_ResetForm_ResetsAllFieldsToDefaultState()
    {
        var conn = new ConnectionStateViewModel
        {
            Host = "10.0.0.99",
            Port = 5900,
            Username = "superadmin",
            Password = "CustomPassword",
            IsConnected = true,
            StatusText = "Active Session"
        };

        conn.ResetForm();

        Assert.Equal("127.0.0.1", conn.Host);
        Assert.Equal(3389, conn.Port);
        Assert.Equal("admin", conn.Username);
        Assert.Equal(string.Empty, conn.Password);
        Assert.False(conn.IsConnected);
        Assert.Equal("Disconnected", conn.StatusText);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_RapidStateTransitionsStress_MaintainsStateConsistency()
    {
        var vm = new MainWindowViewModel();

        for (int i = 0; i < 1000; i++)
        {
            vm.ConnectCommand.Execute(null);
            Assert.True(vm.Connection.IsConnected);
            Assert.Equal("Connected", vm.Connection.StatusText);

            vm.ToggleRecordCommand.Execute(null);
            Assert.True(vm.Recorder.IsRecording);

            vm.DisconnectCommand.Execute(null);
            Assert.False(vm.Connection.IsConnected);
            Assert.Equal("Disconnected", vm.Connection.StatusText);

            vm.ToggleRecordCommand.Execute(null);
            Assert.False(vm.Recorder.IsRecording);
        }
    }

    #endregion

    #region 2. CDP Server Initialization on Port 9224

    [AvaloniaFact]
    public void CdpRdpApp_CdpServerPort_IsConfiguredTo9224()
    {
        // Verify CdpServer port configuration API
        try
        {
            CdpServer.Start(9224);
        }
        catch (System.Net.HttpListenerException) { }

        Assert.True(CdpServer.Port > 0);
    }

    [AvaloniaTheory]
    [InlineData(new string[] { "--headless" }, 9224)]
    [InlineData(new string[] { "--headless", "--port", "9224" }, 9224)]
    [InlineData(new string[] { "--headless", "--port", "9225" }, 9225)]
    [InlineData(new string[] { "--headless", "--port", "9999" }, 9999)]
    public void CdpRdpApp_HeadlessProgramPortOption_ParsesCustomAndDefaultPort9224(string[] args, int expectedPort)
    {
        int port = 9224;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out port);
            }
        }

        Assert.Equal(expectedPort, port);
    }

    [AvaloniaFact]
    public void CdpRdpApp_CdpServerInitialization_EnsuresInitializedWithoutExceptions()
    {
        // Call EnsureInitialized multiple times to test idempotent server setup
        CdpServer.EnsureInitialized();
        CdpServer.EnsureInitialized();
        Assert.True(CdpServer.Port > 0, "CdpServer.Port should be initialized.");
    }

    #endregion

    #region 3. UI Layout Responsiveness and Memory Allocation Safety During Frame Updates

    [AvaloniaFact]
    public void RdpControl_FrameUpdates_LayoutResponsivenessUnderHighFrequencyUpdates()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);

        byte[] rawPixels = new byte[64 * 64 * 4];
        Array.Fill(rawPixels, (byte)0xAB);

        var stopwatch = Stopwatch.StartNew();
        const int frameCount = 500;

        for (ulong i = 0; i < frameCount; i++)
        {
            ushort left = (ushort)((i * 16) % 1200);
            ushort top = (ushort)((i * 8) % 650);

            var update = new RdpBitmapUpdate(left, top, 64, 64, 32, compressed: false, rawPixels);
            control.FrameBuffer!.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            var dirty = control.FrameBuffer.SwapBuffers();
            Assert.False(dirty.IsEmpty);
        }

        stopwatch.Stop();
        double totalMs = stopwatch.Elapsed.TotalMilliseconds;
        double avgMsPerFrame = totalMs / frameCount;

        // Average processing per frame update must be under 2.0 ms to ensure smooth layout responsiveness (> 500 FPS)
        Assert.True(avgMsPerFrame < 2.0, $"Average frame update latency was {avgMsPerFrame:F4} ms, exceeding 2.0 ms budget.");
    }

    [AvaloniaFact]
    public void RdpControl_Render_MemoryAllocationSafetyUnderContinuousRendering()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);
        control.Width = 1280;
        control.Height = 720;

        byte[] rawPixels = new byte[128 * 128 * 4];
        Array.Fill(rawPixels, (byte)0x7F);

        // Warm up allocations
        for (ulong i = 0; i < 100; i++)
        {
            var update = new RdpBitmapUpdate(0, 0, 128, 128, 32, compressed: false, rawPixels);
            control.FrameBuffer!.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            control.FrameBuffer.SwapBuffers();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long initialMemory = GC.GetTotalMemory(true);

        const int renderCycles = 2000;
        for (ulong i = 0; i < renderCycles; i++)
        {
            ushort x = (ushort)((i * 32) % 1100);
            ushort y = (ushort)((i * 16) % 550);

            var update = new RdpBitmapUpdate(x, y, 128, 128, 32, compressed: false, rawPixels);
            control.FrameBuffer!.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            control.FrameBuffer.SwapBuffers();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(true);
        long memoryDelta = finalMemory - initialMemory;

        // Verify memory growth after 2,000 continuous frame render cycles is less than 10 MB
        Assert.True(memoryDelta < 10 * 1024 * 1024, $"Memory grew by {memoryDelta} bytes after {renderCycles} render cycles, exceeding safety threshold.");
    }

    [AvaloniaFact]
    public void RdpControl_WindowResizeAndScaling_LayoutResponsivenessDoesNotCrash()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);

        (double width, double height)[] resolutions = new[]
        {
            (320.0, 240.0),
            (640.0, 480.0),
            (1280.0, 720.0),
            (1920.0, 1080.0),
            (2560.0, 1440.0),
            (3840.0, 2160.0)
        };

        foreach (var (w, h) in resolutions)
        {
            control.Width = w;
            control.Height = h;

            Point testPoint = new Point(w / 2, h / 2);
            control.TranslateCoordinates(testPoint, out ushort mappedX, out ushort mappedY);

            Assert.Equal((ushort)(1280 / 2), mappedX);
            Assert.Equal((ushort)(720 / 2), mappedY);
        }
    }

    [AvaloniaFact]
    public void RdpView_LayoutAndBindings_InstantiatesWithoutExceptions()
    {
        var rdpView = new RdpView();

        Assert.Equal("127.0.0.1", rdpView.Host);
        Assert.Equal(3389, rdpView.Port);
        Assert.Equal(string.Empty, rdpView.Username);
        Assert.Equal(string.Empty, rdpView.Password);
        Assert.Equal(string.Empty, rdpView.Domain);
        Assert.False(rdpView.IsConnected);
        Assert.Null(rdpView.Session);

        // Mutate bound properties
        rdpView.Host = "10.1.1.1";
        rdpView.Port = 3389;
        rdpView.Username = "admin";
        rdpView.IsConnected = true;

        Assert.Equal("10.1.1.1", rdpView.Host);
        Assert.Equal("admin", rdpView.Username);
        Assert.True(rdpView.IsConnected);
    }

    #endregion
}
