namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Cdp.Rdp;
using CDP.Rdp.Channels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;
using Avalonia.Headless.XUnit;

public class ChallengerM2Iter5StressTests
{
    private readonly string _tempTestDir;

    public ChallengerM2Iter5StressTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "CDP_M2_Iter5_Challenger_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    // ==================================================================================
    // 1. MULTI-SESSION TAB DISPOSAL & MEMORY LEAK STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task MultiSessionTab_DisposalStress_UnhooksEventsAndReleasesReferences()
    {
        WeakReference tabWeakRef;
        WeakReference sessionWeakRef;

        Func<Task> createAndDisposeTab = async () =>
        {
            var mockSession = new StressTestRdpSession();
            var tab = new RdpSessionTab
            {
                Session = mockSession
            };

            // Trigger events to ensure handlers are attached and executing
            mockSession.TriggerStateChanged(RdpConnectionState.Connected);
            mockSession.TriggerFrameUpdated(1);

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(RdpConnectionState.Connected, tab.ConnectionState);
            Assert.Equal(1, tab.TotalFrames);

            tabWeakRef = new WeakReference(tab);
            sessionWeakRef = new WeakReference(mockSession);

            // Explicit disposal
            tab.Dispose();

            // After disposal, firing event should not update tab or throw
            mockSession.TriggerFrameUpdated(1);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            await Task.CompletedTask;
        };

        tabWeakRef = null!;
        sessionWeakRef = null!;

        await createAndDisposeTab();

        // Perform GC stress collect
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(tabWeakRef.IsAlive, "RdpSessionTab instance leaked memory after Dispose()");
        Assert.False(sessionWeakRef.IsAlive, "IRdpSession instance leaked memory after RdpSessionTab Dispose()");
    }

    [AvaloniaFact]
    public async Task SessionWorkspace_RapidOpenCloseStress_50Sessions_NoLeaks()
    {
        var vm = new SessionWorkspaceViewModel();
        var weakTabs = new List<WeakReference>();

        CreateAndDisconnectWorkspaceSessions(vm, weakTabs);

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);

        // GC Collect stress loop
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        int aliveCount = weakTabs.Count(r => r.IsAlive);
        Assert.Equal(0, aliveCount);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CreateAndDisconnectWorkspaceSessions(SessionWorkspaceViewModel vm, List<WeakReference> weakTabs)
    {
        for (int i = 0; i < 50; i++)
        {
            var profile = new RdpConnectionProfile
            {
                Name = $"Stress Session {i}",
                Host = $"10.0.0.{i + 1}"
            };

            var tab = vm.OpenSession(profile);
            var mockSession = new StressTestRdpSession();
            tab.Session = mockSession;

            mockSession.TriggerStateChanged(RdpConnectionState.Connected);
            mockSession.TriggerFrameUpdated(i % 5 + 1);

            weakTabs.Add(new WeakReference(tab));
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(50, vm.Sessions.Count);

        // Close all sessions
        vm.ExecuteDisconnectAllAsync().GetAwaiter().GetResult();
    }

    [AvaloniaFact]
    public void DoubleDisposal_RdpSessionTab_DoesNotThrow()
    {
        var mockSession = new StressTestRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession
        };

        var exception = Record.Exception(() =>
        {
            tab.Dispose();
            tab.Dispose();
        });

        Assert.Null(exception);
    }

    // ==================================================================================
    // 2. THREAD MARSHALING UNDER TEST CONTEXTS STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task ThreadMarshaling_ParallelBackgroundEvents_UpdatesStateSynchronouslyInTestContext()
    {
        var mockSession = new StressTestRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession
        };

        int tasksCount = 10;
        int eventsPerTask = 50;

        var tasks = new List<Task>();

        for (int t = 0; t < tasksCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int e = 0; e < eventsPerTask; e++)
                {
                    mockSession.TriggerFrameUpdated(1);
                    mockSession.TriggerStateChanged(RdpConnectionState.Connected);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (tab.TotalFrames < tasksCount * eventsPerTask && DateTime.UtcNow < timeout)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(tasksCount * eventsPerTask, tab.TotalFrames);
        Assert.Equal(RdpConnectionState.Connected, tab.ConnectionState);
        Assert.Equal("Connected", tab.Status);
    }

    // ==================================================================================
    // 3. SCALE FACTOR & INFINITY BOUNDS STRESS TESTS
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(double.PositiveInfinity, 50, 50, (ushort)640, (ushort)360)]
    [InlineData(double.NegativeInfinity, 50, 50, (ushort)640, (ushort)360)]
    [InlineData(double.NaN, 50, 50, (ushort)640, (ushort)360)]
    [InlineData(0.0, 50, 50, (ushort)640, (ushort)360)]
    [InlineData(-10.0, 50, 50, (ushort)640, (ushort)360)]
    [InlineData(2.0, 50, 50, (ushort)320, (ushort)180)]
    [InlineData(0.5, 50, 50, (ushort)1279, (ushort)719)]
    public void ScaleFactor_InfinityAndBoundaryValues_TranslatesCoordinatesSafely(
        double scaleFactor, double x, double y, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);
        control.Width = 100;
        control.Height = 100;
        control.ScaleFactor = scaleFactor;

        Point pt = new Point(x, y);
        var exception = Record.Exception(() =>
        {
            control.TranslateCoordinates(pt, out ushort mappedX, out ushort mappedY);
            Assert.Equal(expectedX, mappedX);
            Assert.Equal(expectedY, mappedY);
        });

        Assert.Null(exception);
    }

    [AvaloniaTheory]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, (ushort)1279, (ushort)719)]
    [InlineData(double.NegativeInfinity, double.NegativeInfinity, (ushort)0, (ushort)0)]
    [InlineData(double.NaN, double.NaN, (ushort)0, (ushort)0)]
    [InlineData(double.MaxValue, double.MaxValue, (ushort)1279, (ushort)719)]
    [InlineData(double.MinValue, double.MinValue, (ushort)0, (ushort)0)]
    public void ControlPoint_ExtremeValues_TranslatesToFramebufferBoundsWithoutOverflow(
        double posX, double posY, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);
        control.Width = 100;
        control.Height = 100;
        control.ScaleFactor = 1.0;

        Point pt = new Point(posX, posY);
        control.TranslateCoordinates(pt, out ushort mappedX, out ushort mappedY);

        Assert.Equal(expectedX, mappedX);
        Assert.Equal(expectedY, mappedY);
    }

    // ==================================================================================
    // 4. PROFILE IMPORT/EXPORT ENCRYPTION STRESS TESTS
    // ==================================================================================

    [Fact]
    public async Task ProfileExportImport_EncryptionStress_SpecialCharactersUnicodeAndLongPasswords()
    {
        string exportFile = Path.Combine(_tempTestDir, "stress_export.json");
        var storage = new ProfileStorageService(Path.Combine(_tempTestDir, "storage.json"));
        var credService = new CredentialProtectionService();
        var vm = new ProfilesViewModel(storage, credService);

        string passwordUnicode = "🔒Pä$$wörd_日本語_123!@#$%^&*()_+-=[]{}|;':\",./<>?";
        string longPassword = new string('A', 5000);

        var profile1 = new RdpConnectionProfile { Name = "Unicode Profile", Host = "10.0.0.1", Password = passwordUnicode };
        var profile2 = new RdpConnectionProfile { Name = "Long Pass Profile", Host = "10.0.0.2", Password = longPassword };
        var profile3 = new RdpConnectionProfile { Name = "Empty Pass Profile", Host = "10.0.0.3", Password = string.Empty };

        vm.Profiles.Add(profile1);
        vm.Profiles.Add(profile2);
        vm.Profiles.Add(profile3);

        // Export
        await vm.ExecuteExportProfilesAsync(exportFile);

        Assert.True(File.Exists(exportFile));
        string rawJson = await File.ReadAllTextAsync(exportFile);

        // Verify plain text passwords are NOT in JSON file
        Assert.DoesNotContain(passwordUnicode, rawJson);
        Assert.DoesNotContain(longPassword, rawJson);

        // Import back into clean VM
        vm.Profiles.Clear();
        await vm.ExecuteImportProfilesAsync(exportFile);

        Assert.Equal(3, vm.Profiles.Count);

        var imported1 = vm.Profiles.First(p => p.Name == "Unicode Profile");
        var imported2 = vm.Profiles.First(p => p.Name == "Long Pass Profile");
        var imported3 = vm.Profiles.First(p => p.Name == "Empty Pass Profile");

        Assert.Equal(passwordUnicode, imported1.Password);
        Assert.Equal(longPassword, imported2.Password);
        Assert.Equal(string.Empty, imported3.Password);
    }

    [Fact]
    public async Task ProfileImport_CorruptedOrNonEncryptedPasswords_HandlesGracefully()
    {
        string importFile = Path.Combine(_tempTestDir, "corrupted_import.json");
        var storage = new ProfileStorageService(Path.Combine(_tempTestDir, "storage.json"));
        var credService = new CredentialProtectionService();
        var vm = new ProfilesViewModel(storage, credService);

        // JSON with plain-text password (no ENC: prefix) and invalid ENC: string
        string json = @"[
            { ""Id"": ""1"", ""Name"": ""Plain Profile"", ""Host"": ""10.0.0.1"", ""Password"": ""PlainSecret123"" },
            { ""Id"": ""2"", ""Name"": ""Invalid Enc Profile"", ""Host"": ""10.0.0.2"", ""Password"": ""ENC:InvalidBase64!!!"" }
        ]";

        await File.WriteAllTextAsync(importFile, json);

        var exception = Record.ExceptionAsync(async () =>
        {
            await vm.ExecuteImportProfilesAsync(importFile);
        });

        Assert.Null(await exception);
        Assert.Equal(2, vm.Profiles.Count);

        // Unprotect on non-ENC returns original string
        Assert.Equal("PlainSecret123", vm.Profiles[0].Password);
        Assert.Equal("ENC:InvalidBase64!!!", vm.Profiles[1].Password);
    }

    private class StressTestRdpSession : IRdpSession
    {
        public RdpConnectionState State { get; private set; } = RdpConnectionState.Disconnected;
        public RdpSessionOptions Options => new RdpSessionOptions();
        public StaticVirtualChannelManager? StaticVirtualChannels => null;
        public DynamicVirtualChannelManager? DynamicVirtualChannels => null;

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public void TriggerStateChanged(RdpConnectionState newState)
        {
            var oldState = State;
            State = newState;
            StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(oldState, newState));
        }

        public void TriggerFrameUpdated(int dirtyRectCount)
        {
            var bitmaps = new List<RdpBitmapUpdate>();
            for (int i = 0; i < dirtyRectCount; i++)
            {
                bitmaps.Add(new RdpBitmapUpdate(0, 0, 10, 10, 32, false, new byte[400]));
            }
            FrameUpdated?.Invoke(this, new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, bitmaps));
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
