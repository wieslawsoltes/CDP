namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

public class Milestone2Iteration4EmpiricalStressHarness
{
    private readonly string _tempTestDir;

    public Milestone2Iteration4EmpiricalStressHarness()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "M2_Iter4_StressHarness_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    private static Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>> CreateMockTransportFactory(int connectDelayMs = 0)
    {
        return async (opts, ct) =>
        {
            if (connectDelayMs > 0)
            {
                await Task.Delay(connectDelayMs, ct);
            }
            var stream = new MemoryStream();
            IRdpSecurityTransport transport = new PlainRdpSecurityTransport(stream);
            return transport;
        };
    }

    // ==================================================================================
    // AREA 1: MULTI-SESSION TAB LIFECYCLE STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task StressTest_MultiSessionTabLifecycle_HighVolumeCreationAndOutofOrderDisposal()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        await workspaceVM.ExecuteDisconnectAllAsync();
        Assert.Empty(workspaceVM.Sessions);

        var transportFactory = CreateMockTransportFactory();
        int totalTabs = 50;
        var tabs = new List<RdpSessionTab>();

        // 1. Rapidly create 50 sessions
        for (int i = 0; i < totalTabs; i++)
        {
            var tab = workspaceVM.OpenSession(new RdpConnectionProfile
            {
                Name = $"Stress Session {i + 1}",
                Host = $"10.0.0.{i + 1}",
                Port = 3389 + i,
                Username = $"user{i}"
            }, transportFactory);
            tabs.Add(tab);
        }

        Assert.Equal(totalTabs, workspaceVM.Sessions.Count);
        Assert.Equal(tabs.Last(), workspaceVM.SelectedSession);
        Assert.True(tabs.Last().IsActive);

        // Allow background connection tasks to complete
        await Task.Delay(100);

        // 2. Out-of-order disposal: Close odd-indexed tabs
        for (int i = 1; i < totalTabs; i += 2)
        {
            await workspaceVM.ExecuteCloseSessionAsync(tabs[i]);
        }

        Assert.Equal(25, workspaceVM.Sessions.Count);
        foreach (var closedTab in tabs.Where((t, idx) => idx % 2 != 0))
        {
            Assert.DoesNotContain(closedTab, workspaceVM.Sessions);
            Assert.Equal("Disconnected", closedTab.Status);
            Assert.Null(closedTab.Session);
        }

        // 3. Double-close: re-closing an already closed tab should execute gracefully without exception
        var alreadyClosedTab = tabs[1];
        var doubleCloseEx = await Record.ExceptionAsync(() => workspaceVM.ExecuteCloseSessionAsync(alreadyClosedTab));
        Assert.Null(doubleCloseEx);

        // 4. Disconnect all remaining tabs
        await workspaceVM.ExecuteDisconnectAllAsync();
        Assert.Empty(workspaceVM.Sessions);
        Assert.Null(workspaceVM.SelectedSession);
    }

    [AvaloniaFact]
    public async Task StressTest_MultiSessionTabLifecycle_RaceCondition_ConnectAsyncVsImmediateClose()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        await workspaceVM.ExecuteDisconnectAllAsync();

        // Slow mock transport that delays 100ms during connect
        var slowTransportFactory = CreateMockTransportFactory(connectDelayMs: 100);

        for (int i = 0; i < 20; i++)
        {
            var tab = workspaceVM.OpenSession(new RdpConnectionProfile
            {
                Name = $"Race Session {i}",
                Host = "127.0.0.1"
            }, slowTransportFactory);

            // Immediately close tab without waiting for connect to finish
            await workspaceVM.ExecuteCloseSessionAsync(tab);
        }

        Assert.Empty(workspaceVM.Sessions);
        Assert.Null(workspaceVM.SelectedSession);

        // Wait to verify no unhandled background exceptions occur
        await Task.Delay(150);
    }

    [AvaloniaFact]
    public async Task StressTest_MultiSessionTabLifecycle_EventSubscriptionCleanupOnDisposal()
    {
        var tab = new RdpSessionTab();
        var mockSession = new MockRdpSession();
        tab.Session = mockSession;

        Assert.Equal(0L, tab.TotalFrames);

        // Trigger frame update before disposal
        mockSession.RaiseFrameUpdated(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>()));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1L, tab.TotalFrames);

        // Disconnect and dispose tab
        await tab.DisconnectSessionAsync();
        tab.Dispose();

        Assert.Null(tab.Session);

        // Trigger frame update on old session object — disposed tab should NOT receive it
        mockSession.RaiseFrameUpdated(new RdpFrameUpdateEventArgs(2, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>()));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // TotalFrames must remain 1
        Assert.Equal(1L, tab.TotalFrames);
    }

    [AvaloniaFact]
    public void StressTest_ActiveTabSelectionInvariants_IsActiveStateTransitions()
    {
        var workspaceVM = new SessionWorkspaceViewModel();
        var tab1 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "T1" });
        var tab2 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "T2" });
        var tab3 = workspaceVM.OpenSession(new RdpConnectionProfile { Name = "T3" });

        Assert.False(tab1.IsActive);
        Assert.False(tab2.IsActive);
        Assert.True(tab3.IsActive);

        // Switch selection to Tab 1
        workspaceVM.SelectedSession = tab1;
        Assert.True(tab1.IsActive);
        Assert.False(tab2.IsActive);
        Assert.False(tab3.IsActive);

        // Switch selection to null
        workspaceVM.SelectedSession = null;
        Assert.False(tab1.IsActive);
        Assert.False(tab2.IsActive);
        Assert.False(tab3.IsActive);
    }

    // ==================================================================================
    // AREA 2: PROFILE JSON SERIALIZATION & DESERIALIZATION STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task StressTest_ProfileStorage_MalformedJson_HandlesGracefullyWithoutCrashing()
    {
        string[] malformedPayloads = new[]
        {
            "{ invalid json }",
            "not a json at all",
            "{\"Id\": 123, \"Name\": }",
            "null",
            "[{\"Id\": \"1\", \"Name\": \"P1\"}, { invalid_object }]"
        };

        for (int i = 0; i < malformedPayloads.Length; i++)
        {
            string filePath = Path.Combine(_tempTestDir, $"malformed_{i}.json");
            await File.WriteAllTextAsync(filePath, malformedPayloads[i]);

            var storage = new ProfileStorageService(filePath);
            var ex = await Record.ExceptionAsync(async () =>
            {
                var profiles = await storage.LoadProfilesAsync();
                Assert.NotNull(profiles);
            });

            Assert.Null(ex); // Must not throw unhandled exception
        }
    }

    [AvaloniaFact]
    public async Task StressTest_ProfileStorage_ExtremeValues_PathTraversalAndUnicode()
    {
        string filePath = Path.Combine(_tempTestDir, "extreme_profiles.json");
        await File.WriteAllTextAsync(filePath, "[]");

        var storage = new ProfileStorageService(filePath);
        string longString = new string('A', 20000);

        var extremeProfiles = new List<RdpConnectionProfile>
        {
            new RdpConnectionProfile
            {
                Id = "../../etc/passwd\0file.txt",
                Name = "Unicode: 🚀🔥💻⚡️ | 日本語 | <script>alert('xss')</script> | " + longString,
                Host = "::1",
                Port = 65535,
                Username = "admin\\domain' OR '1'='1",
                Password = "P@ssw0rd!#$&*()_+~|}{[]:;?><,./-=",
                Domain = "DOMAIN\\SUBDOMAIN",
                Width = 7680,
                Height = 4320,
                ColorDepth = 32,
                IsAutoConnect = true,
                LastConnected = DateTime.UtcNow
            },
            new RdpConnectionProfile
            {
                Id = "p-negative",
                Name = "Negative Specs",
                Host = "",
                Port = -1,
                Width = 0,
                Height = -100,
                ColorDepth = -32
            }
        };

        await storage.SaveProfilesAsync(extremeProfiles);

        var loaded = await storage.LoadProfilesAsync();
        Assert.Equal(2, loaded.Count);

        var p1 = loaded[0];
        Assert.Equal("../../etc/passwd\0file.txt", p1.Id);
        Assert.Contains("🚀🔥💻⚡️", p1.Name);
        Assert.Equal("P@ssw0rd!#$&*()_+~|}{[]:;?><,./-=", p1.Password);

        var p2 = loaded[1];
        Assert.Equal(-1, p2.Port);
        Assert.Equal(0, p2.Width);
        Assert.Equal(-100, p2.Height);
    }

    [AvaloniaFact]
    public async Task StressTest_ProfileStorage_EncryptedPassword_CorruptedCiphertext_HandledSafely()
    {
        string filePath = Path.Combine(_tempTestDir, "corrupted_enc.json");
        string jsonWithCorruptedEnc = @"[
            {
                ""Id"": ""c1"",
                ""Name"": ""Corrupted Enc Base64"",
                ""Host"": ""127.0.0.1"",
                ""Password"": ""ENC:@@@INVALID_BASE64@@@""
            },
            {
                ""Id"": ""c2"",
                ""Name"": ""Corrupted Enc Content"",
                ""Host"": ""127.0.0.1"",
                ""Password"": ""ENC:dGVzdA==""
            },
            {
                ""Id"": ""c3"",
                ""Name"": ""Plain Password Legacy"",
                ""Host"": ""127.0.0.1"",
                ""Password"": ""UnencryptedSecret123""
            }
        ]";

        await File.WriteAllTextAsync(filePath, jsonWithCorruptedEnc);

        var storage = new ProfileStorageService(filePath);
        var loaded = await storage.LoadProfilesAsync();

        Assert.Equal(3, loaded.Count);
        Assert.Equal("ENC:@@@INVALID_BASE64@@@", loaded[0].Password); // Falls back to raw text on corrupt base64
        Assert.Equal("UnencryptedSecret123", loaded[2].Password); // Unencrypted legacy remains unchanged
    }

    [AvaloniaFact]
    public async Task StressTest_ProfileStorage_ConcurrentOperations_ThreadSafetyUnderLock()
    {
        string filePath = Path.Combine(_tempTestDir, "concurrent_storage.json");
        await File.WriteAllTextAsync(filePath, "[]");

        var storage = new ProfileStorageService(filePath);
        int concurrentTasks = 20;

        var tasks = new List<Task>();
        for (int i = 0; i < concurrentTasks; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var profile = new RdpConnectionProfile
                {
                    Id = $"prof-{index}",
                    Name = $"Concurrent Profile {index}",
                    Host = $"10.0.0.{index}"
                };
                await storage.AddProfileAsync(profile);
                await storage.LoadProfilesAsync();
                await storage.UpdateProfileAsync(profile);
            }));
        }

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);

        var finalProfiles = await storage.LoadProfilesAsync();
        Assert.NotEmpty(finalProfiles);
    }

    // ==================================================================================
    // AREA 3: DISPLAY RESOLUTION SCALING CALCULATION IN RDPCONTROL STRESS TESTS
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(1000, 500, 1.0, 1280, 720, 500, 250, 640, 360)]
    [InlineData(1000, 500, 2.0, 1280, 720, 500, 250, 320, 180)]
    [InlineData(1000, 500, 0.5, 1280, 720, 500, 250, 1279, 719)]
    [InlineData(1920, 1080, 1.5, 1920, 1080, 1440, 810, 960, 540)]
    public void StressTest_RdpControl_TranslateCoordinates_ScalingOracle(
        double controlWidth, double controlHeight, double scaleFactor,
        int fbWidth, int fbHeight, double inputX, double inputY, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl
        {
            Width = controlWidth,
            Height = controlHeight,
            ScaleFactor = scaleFactor
        };
        control.InitFrameBuffer(fbWidth, fbHeight);

        Point inputPoint = new Point(inputX, inputY);
        control.TranslateCoordinates(inputPoint, out ushort actualX, out ushort actualY);

        Assert.Equal(expectedX, actualX);
        Assert.Equal(expectedY, actualY);
    }

    [AvaloniaTheory]
    [InlineData(-100.0, -50.0, (ushort)0, (ushort)0)] // Negative inputs clip to 0
    [InlineData(99999.0, 99999.0, (ushort)1279, (ushort)719)] // Overflow inputs clip to max fb bounds
    [InlineData(double.NaN, 250.0, (ushort)0, (ushort)360)] // NaN input X maps to 0
    [InlineData(500.0, double.NaN, (ushort)640, (ushort)0)] // NaN input Y maps to 0
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, (ushort)1279, (ushort)719)]
    [InlineData(double.NegativeInfinity, double.NegativeInfinity, (ushort)0, (ushort)0)]
    public void StressTest_RdpControl_TranslateCoordinates_BoundaryAndExtremePointInputs(
        double inputX, double inputY, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = 1.0
        };
        control.InitFrameBuffer(1280, 720);

        Point inputPoint = new Point(inputX, inputY);
        var ex = Record.Exception(() => control.TranslateCoordinates(inputPoint, out ushort actualX, out ushort actualY));
        Assert.Null(ex);

        control.TranslateCoordinates(inputPoint, out ushort x, out ushort y);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [AvaloniaTheory]
    [InlineData(0.0)]
    [InlineData(-2.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void StressTest_RdpControl_TranslateCoordinates_InvalidScaleFactors_FallbackToDefault1(double invalidScale)
    {
        var control = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = invalidScale
        };
        control.InitFrameBuffer(1000, 500);

        Point center = new Point(500, 250);
        control.TranslateCoordinates(center, out ushort x, out ushort y);

        // Fallback scale 1.0 => (500, 250) -> (500, 250)
        Assert.Equal((ushort)500, x);
        Assert.Equal((ushort)250, y);
    }

    [AvaloniaFact]
    public void StressTest_RdpControl_TranslateCoordinates_ZeroOrNegativeControlBounds_DoesNotThrowDivideByZero()
    {
        var control = new RdpControl
        {
            Width = 0,
            Height = 0,
            ScaleFactor = 1.0
        };
        control.InitFrameBuffer(1280, 720);

        Point inputPoint = new Point(100, 100);
        var ex = Record.Exception(() => control.TranslateCoordinates(inputPoint, out ushort x, out ushort y));
        Assert.Null(ex);
    }

    // ==================================================================================
    // AREA 4: THREAD DISPATCHING & UI THREAD MARSHALING STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task StressTest_ThreadDispatching_BackgroundThreadOperations_ExecuteSafely()
    {
        var tab = new RdpSessionTab();

        // Perform operations from background worker thread
        await Task.Run(async () =>
        {
            tab.Title = "Background Title";
            tab.Host = "10.0.0.5";
            tab.Port = 3389;

            var transportFactory = CreateMockTransportFactory();
            await tab.ConnectSessionAsync(transportFactory);
            await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltTab);
            await tab.DisconnectSessionAsync();
        });

        Assert.Equal("Background Title", tab.Title);
        Assert.Equal("10.0.0.5", tab.Host);
        Assert.Equal("Disconnected", tab.Status);
    }

    [AvaloniaFact]
    public async Task StressTest_ThreadDispatching_HighFrequencyFrameUpdates_ConcurrentWithDisposal()
    {
        var tab = new RdpSessionTab();
        var mockSession = new MockRdpSession();
        tab.Session = mockSession;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500);

        // Background thread hammering tab with frame updates
        var producerTask = Task.Run(() =>
        {
            ulong frameId = 1;
            while (!cts.IsCancellationRequested)
            {
                var args = new RdpFrameUpdateEventArgs(frameId++, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>());
                mockSession.RaiseFrameUpdated(args);
                Thread.Sleep(1);
            }
        });

        await Task.Delay(100);

        // Concurrently disconnect tab
        await tab.DisconnectSessionAsync();
        tab.Dispose();

        await producerTask;

        Assert.Null(tab.Session);
    }

    // Mock Session class for testing
    private class MockRdpSession : IRdpSession
    {
        public RdpConnectionState State => RdpConnectionState.Connected;
        public RdpSessionOptions Options => new RdpSessionOptions();
        public StaticVirtualChannelManager? StaticVirtualChannels => null;
        public DynamicVirtualChannelManager? DynamicVirtualChannels => null;

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public void RaiseFrameUpdated(RdpFrameUpdateEventArgs args)
        {
            FrameUpdated?.Invoke(this, args);
        }

        public void RaiseStateChanged(RdpConnectionStateChangedEventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
