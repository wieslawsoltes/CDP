using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Headless.XUnit;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Rendering;
using CDP.Rdp.Security;
using CDP.Rdp.Channels;
using CDP.Rdp.Session;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

namespace CDP.Rdp.Tests.Challenger;

public class RdpEmpiricalStressHarnessM2_4Tests : IDisposable
{
    private readonly string _tempDir;

    public RdpEmpiricalStressHarnessM2_4Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CDP_M2_4_Stress_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempDir))
        {
            Directory.CreateDirectory(_tempDir);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private static Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>> CreateMockTransportFactory(
        bool simulateFailure = false, int delayMs = 0)
    {
        return async (opts, ct) =>
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ct);
            }

            if (simulateFailure)
            {
                throw new InvalidOperationException("Simulated RDP connection failure");
            }

            var stream = new MemoryStream();
            IRdpSecurityTransport transport = new PlainRdpSecurityTransport(stream);
            return transport;
        };
    }

    // ==================================================================================
    // 1. MULTI-SESSION TAB LIFECYCLE & HIGH CONCURRENCY STRESS
    // ==================================================================================

    [AvaloniaFact]
    public async Task MultiSession_HighConcurrency_50Tabs_RapidCreationAndClosure_MaintainsStateIntegrity()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        await workspaceVM.ExecuteDisconnectAllAsync();

        var transportFactory = CreateMockTransportFactory();
        int totalTabs = 50;
        var createdTabs = new List<RdpSessionTab>();

        for (int i = 0; i < totalTabs; i++)
        {
            var profile = new RdpConnectionProfile
            {
                Name = $"Session {i}",
                Host = $"192.168.1.{i + 1}",
                Port = 3389 + i
            };
            var tab = workspaceVM.OpenSession(profile, transportFactory);
            createdTabs.Add(tab);
        }

        Assert.Equal(totalTabs, workspaceVM.Sessions.Count);
        Assert.Equal(createdTabs.Last(), workspaceVM.SelectedSession);

        await Task.Delay(200);

        // Close even indexed tabs
        for (int i = totalTabs - 2; i >= 0; i -= 2)
        {
            await workspaceVM.ExecuteCloseSessionAsync(createdTabs[i]);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(25, workspaceVM.Sessions.Count);

        // Disconnect all
        await workspaceVM.ExecuteDisconnectAllAsync();
        Assert.Empty(workspaceVM.Sessions);
        Assert.Null(workspaceVM.SelectedSession);
    }

    [AvaloniaFact]
    public async Task MultiSession_DisposeTabDuringActiveConnect_CancelsConnectionWithoutCrash()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        var transportFactory = CreateMockTransportFactory(delayMs: 2000); // 2 sec delay

        var profile = new RdpConnectionProfile { Name = "Slow Connect Server", Host = "10.0.0.1" };
        var tab = workspaceVM.OpenSession(profile, transportFactory);

        // Immediately close tab while connect is pending in background
        await Task.Delay(50);
        await workspaceVM.ExecuteCloseSessionAsync(tab);

        // Wait to verify no unhandled exceptions fire
        await Task.Delay(300);

        Assert.DoesNotContain(tab, workspaceVM.Sessions);
        Assert.Equal("Disconnected", tab.Status);
    }

    [AvaloniaFact]
    public async Task MultiSession_RapidActiveTabSwitching_UnderPushedFrames_NoDeadlocks()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        var transportFactory = CreateMockTransportFactory();

        var tab1 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 1" }, transportFactory);
        var tab2 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 2" }, transportFactory);
        var tab3 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "Tab 3" }, transportFactory);

        await Task.Delay(100);

        // Push frames continuously from background threads
        using var cts = new CancellationTokenSource();
        var pushTask = Task.Run(async () =>
        {
            var tabs = new[] { tab1, tab2, tab3 };
            int step = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                foreach (var tab in tabs)
                {
                    if (tab.Session is RdpClient client)
                    {
                        var update = new RdpFrameUpdateEventArgs((ulong)step++, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>());
                        client.RaiseFrameUpdatedForTesting(update);
                    }
                }
                await Task.Delay(10);
            }
        });

        // Switch selected session back and forth rapidly on UI thread
        for (int i = 0; i < 30; i++)
        {
            workspaceVM.SelectedSession = (i % 3 == 0) ? tab1 : (i % 3 == 1) ? tab2 : tab3;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        cts.Cancel();
        await pushTask;

        await workspaceVM.ExecuteDisconnectAllAsync();
    }

    [AvaloniaFact]
    public async Task RdpSessionTab_AutoReconnect_HonorsMaxAttemptsAndBackoffCancellation()
    {
        var transportFactory = CreateMockTransportFactory(simulateFailure: true);

        var tab = new RdpSessionTab
        {
            Host = "127.0.0.1",
            AutoReconnect = true,
            MaxReconnectAttempts = 2
        };

        // Attempt connect (will fail and trigger auto-reconnect)
        await tab.ConnectSessionAsync(transportFactory);

        Assert.Equal(RdpConnectionState.Faulted, tab.ConnectionState);
        Assert.Equal("Faulted", tab.Status);

        // Disconnect immediately to cancel reconnect backoff
        await tab.DisconnectSessionAsync();

        Assert.Equal(RdpConnectionState.Disconnected, tab.ConnectionState);
        Assert.Equal("Disconnected", tab.Status);
    }

    // ==================================================================================
    // 2. PROFILE JSON SERIALIZATION/DESERIALIZATION EDGE CASES & CONCURRENCY
    // ==================================================================================

    [Fact]
    public async Task ProfileStorage_ExtremeDataTypesAndStrings_RoundtripsWithoutTruncationOrCorrupt()
    {
        string filePath = Path.Combine(_tempDir, "extreme_profiles.json");
        var storage = new ProfileStorageService(filePath);

        string longName = new string('A', 10000);
        string longPassword = "P@ss!" + new string('X', 5000) + "#2026";
        string unicodeHost = "rdp-🚀-server.internal.domain";

        var extremeProfile = new RdpConnectionProfile
        {
            Id = "extreme-1",
            Name = longName,
            Host = unicodeHost,
            Port = 65535,
            Username = "admin_😀_user",
            Password = longPassword,
            Domain = "DOMAIN\\SUBDOMAIN",
            Width = 7680,
            Height = 4320,
            ColorDepth = 32,
            IsAutoConnect = true,
            LastConnected = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        await storage.SaveProfilesAsync(new[] { extremeProfile });

        var loaded = await storage.LoadProfilesAsync();
        Assert.Single(loaded);

        var p = loaded[0];
        Assert.Equal("extreme-1", p.Id);
        Assert.Equal(longName, p.Name);
        Assert.Equal(unicodeHost, p.Host);
        Assert.Equal(65535, p.Port);
        Assert.Equal("admin_😀_user", p.Username);
        Assert.Equal(longPassword, p.Password);
        Assert.Equal("DOMAIN\\SUBDOMAIN", p.Domain);
        Assert.Equal(7680, p.Width);
        Assert.Equal(4320, p.Height);
        Assert.Equal(32, p.ColorDepth);
        Assert.True(p.IsAutoConnect);
    }

    [Fact]
    public async Task ProfileStorage_CorruptedOrInvalidJsonOnDisk_ReturnsDefaultProfilesWithoutCrashing()
    {
        string filePath = Path.Combine(_tempDir, "corrupted_profiles.json");
        await File.WriteAllTextAsync(filePath, "{ INVALID JSON PAYLOAD: [1, 2, 3, ");

        var storage = new ProfileStorageService(filePath);

        var profiles = await storage.LoadProfilesAsync();

        Assert.NotNull(profiles);
        // Returns default profiles on invalid json for standard non-custom or falls back
        Assert.NotEmpty(profiles);
    }

    [Fact]
    public async Task ProfileStorage_ConcurrentReadWriteAccess_ProtectedByFileLock()
    {
        string filePath = Path.Combine(_tempDir, "concurrent_profiles.json");
        var storage = new ProfileStorageService(filePath);

        // Initial save
        await storage.SaveProfilesAsync(ProfileStorageService.GetDefaultProfiles());

        int count = 20;
        var tasks = new List<Task>();

        for (int i = 0; i < count; i++)
        {
            int index = i;
            if (index % 2 == 0)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await storage.AddProfileAsync(new RdpConnectionProfile
                    {
                        Id = $"concurrent-{index}",
                        Name = $"Concurrent {index}",
                        Host = "10.0.0.1"
                    });
                }));
            }
            else
            {
                tasks.Add(Task.Run(async () =>
                {
                    var profiles = await storage.LoadProfilesAsync();
                    Assert.NotNull(profiles);
                }));
            }
        }

        await Task.WhenAll(tasks);

        var finalProfiles = await storage.LoadProfilesAsync();
        Assert.True(finalProfiles.Count >= 3);
    }

    // ==================================================================================
    // 3. DISPLAY RESOLUTION SCALING CALCULATION IN RDPCONTROL
    // ==================================================================================

    [Fact]
    public void DisplayScaling_TranslateCoordinates_BoundaryAndExtremeInputs_ClampsSafely()
    {
        var control = new RdpControl
        {
            Width = 1920,
            Height = 1080,
            ScaleFactor = 1.0
        };
        control.InitFrameBuffer(1920, 1080);

        // Test Origin (0, 0)
        control.TranslateCoordinates(new Point(0, 0), out ushort x0, out ushort y0);
        Assert.Equal(0, x0);
        Assert.Equal(0, y0);

        // Test Bottom-Right Max Bound (1920, 1080) -> map to fbWidth - 1, fbHeight - 1 (1919, 1079)
        control.TranslateCoordinates(new Point(1920, 1080), out ushort xMax, out ushort yMax);
        Assert.Equal(1919, xMax);
        Assert.Equal(1079, yMax);

        // Test Out of Bounds Positive (2000, 1500) -> clamped to 1919, 1079
        control.TranslateCoordinates(new Point(2000, 1500), out ushort xOver, out ushort yOver);
        Assert.Equal(1919, xOver);
        Assert.Equal(1079, yOver);

        // Test Negative Coordinates (-50, -100) -> clamped to 0, 0
        control.TranslateCoordinates(new Point(-50, -100), out ushort xNeg, out ushort yNeg);
        Assert.Equal(0, xNeg);
        Assert.Equal(0, yNeg);

        // Test Infinity
        control.TranslateCoordinates(new Point(double.PositiveInfinity, double.PositiveInfinity), out ushort xInf, out ushort yInf);
        Assert.Equal(1919, xInf);
        Assert.Equal(1079, yInf);

        // Test Negative Infinity / NaN
        control.TranslateCoordinates(new Point(double.NegativeInfinity, double.NaN), out ushort xNaN, out ushort yNaN);
        Assert.Equal(0, xNaN);
        Assert.Equal(0, yNaN);
    }

    [Fact]
    public void DisplayScaling_TranslateCoordinates_ScaleFactorEdgeCases_BehavesPredictably()
    {
        var control = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = 2.0 // Viewport scaled 2x
        };
        control.InitFrameBuffer(1000, 500);

        // A click at control coordinate (1000, 500) with scale 2.0 maps via (X / (width * scale)) * fbWidth = (1000 / 2000) * 1000 = 500
        control.TranslateCoordinates(new Point(1000, 500), out ushort x, out ushort y);
        Assert.Equal(500, x);
        Assert.Equal(250, y);

        // Extreme scale factor 10.0
        control.ScaleFactor = 10.0;
        control.TranslateCoordinates(new Point(500, 250), out ushort xExt, out ushort yExt);
        Assert.Equal(50, xExt);
        Assert.Equal(25, yExt);
    }

    // ==================================================================================
    // 4. THREAD DISPATCHING & EVENT NOTIFICATION SAFETY
    // ==================================================================================

    [AvaloniaFact]
    public async Task ThreadDispatching_MultithreadedFrameEvents_ProcessedSafelyOnUIThread()
    {
        var tab = new RdpSessionTab();
        var mockSession = new MockRdpSession();
        tab.Session = mockSession;

        int frameCount = 100;
        var tasks = new List<Task>();

        // Fire 100 frame updates from 10 parallel background threads
        for (int t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                MethodInfo? onFrameUpdated = typeof(RdpSessionTab).GetMethod("OnFrameUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 10; i++)
                {
                    var args = new RdpFrameUpdateEventArgs((ulong)i, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>());
                    onFrameUpdated?.Invoke(tab, new object?[] { mockSession, args });
                }
            }));
        }

        await Task.WhenAll(tasks);
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (tab.TotalFrames < frameCount && DateTime.UtcNow < timeout)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        for (int i = 0; i < 5; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Assert.Equal(frameCount, tab.TotalFrames);
    }

    private class MockRdpSession : IRdpSession
    {
        public RdpConnectionState State => RdpConnectionState.Connected;
        public RdpSessionOptions Options => new RdpSessionOptions();
        public StaticVirtualChannelManager? StaticVirtualChannels => null;
        public DynamicVirtualChannelManager? DynamicVirtualChannels => null;

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
