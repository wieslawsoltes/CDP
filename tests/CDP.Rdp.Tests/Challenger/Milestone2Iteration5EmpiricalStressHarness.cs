namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Diagnostics.Cdp.Rdp;
using CDP.Rdp.Channels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Session;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

public class Milestone2Iteration5EmpiricalStressHarness
{
    private readonly string _tempTestDir;

    public Milestone2Iteration5EmpiricalStressHarness()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "WindowsRdpApp_M2I5_Stress_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    // ==================================================================================
    // 1. Multi-Session Tab Disposal Memory Leaks Empirical Challenges
    // ==================================================================================

    [AvaloniaFact]
    public void TabDisposal_DirectDispose_DemonstratesNullSessionBug()
    {
        // Bug Hypothesis: When RdpSessionTab.Dispose() is called directly,
        // it sets _session = null BEFORE calling DisconnectSessionAsync().
        // Consequently, DisconnectSessionAsync sees Session == null and fails to disconnect/dispose the underlying IRdpSession.

        var tab = new RdpSessionTab
        {
            Title = "Leak Test Tab",
            Host = "127.0.0.1",
            Port = 3389
        };

        var mockSession = new DummyTestRdpSession();
        tab.Session = mockSession;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(tab.Session);
        Assert.False(mockSession.IsDisposed);
        Assert.False(mockSession.IsDisconnected);

        // Act: Dispose tab directly
        tab.Dispose();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Empirical assertion check:
        bool sessionWasCleanedUp = mockSession.IsDisposed || mockSession.IsDisconnected;

        Assert.True(sessionWasCleanedUp, "CRITICAL BUG CONFIRMED: RdpSessionTab.Dispose() sets _session=null before calling DisconnectSessionAsync(), leaving underlying IRdpSession undisposed and leaking resources.");
    }

    [AvaloniaFact]
    public async Task MultiSessionTabDisposal_RapidCycle_MemoryLeakStressTest()
    {
        var tabReferences = new List<WeakReference>();
        var sessionReferences = new List<WeakReference>();

        CreateAndDisposeTabsAndSessions(tabReferences, sessionReferences);

        // GC Collect stress loop
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        int aliveTabs = tabReferences.Count(r => r.IsAlive);
        int aliveSessions = sessionReferences.Count(r => r.IsAlive);

        Assert.Equal(0, aliveTabs);
        Assert.Equal(0, aliveSessions);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CreateAndDisposeTabsAndSessions(List<WeakReference> tabReferences, List<WeakReference> sessionReferences)
    {
        for (int i = 0; i < 50; i++)
        {
            var session = new DummyTestRdpSession();
            var tab = new RdpSessionTab
            {
                Title = $"Tab {i}",
                Session = session
            };

            tabReferences.Add(new WeakReference(tab));
            sessionReferences.Add(new WeakReference(session));

            tab.Dispose();
            session.Dispose();
        }
    }

    // ==================================================================================
    // 2. Thread Marshaling Under Test Contexts Empirical Challenges
    // ==================================================================================

    [AvaloniaFact]
    public async Task ThreadMarshaling_BackgroundThreadCallbacks_Behaviors()
    {
        var tab = new RdpSessionTab
        {
            Title = "Thread Test Tab",
            Host = "127.0.0.1"
        };

        var mockSession = new DummyTestRdpSession();
        tab.Session = mockSession;

        bool stateChangedHandled = false;
        tab.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RdpSessionTab.Status))
            {
                stateChangedHandled = true;
            }
        };

        // Fire state change from ThreadPool background thread
        await Task.Run(() =>
        {
            mockSession.RaiseStateChanged(RdpConnectionState.Connected);
        });

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Under test context, stateChangedHandled should execute without throwing thread marshaling errors
        Assert.True(stateChangedHandled);
        Assert.Equal("Connected", tab.Status);
    }

    [AvaloniaFact]
    public async Task ThreadMarshaling_FrameUpdateFromThreadPool_CalculatesFpsSafely()
    {
        var tab = new RdpSessionTab
        {
            Title = "FPS Thread Test"
        };

        var mockSession = new DummyTestRdpSession();
        tab.Session = mockSession;

        // Fire 100 frame updates from 5 parallel background tasks
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 20; i++)
            {
                mockSession.RaiseFrameUpdated();
            }
        }));

        await Task.WhenAll(tasks);

        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (tab.TotalFrames < 100 && DateTime.UtcNow < timeout)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(100, tab.TotalFrames);
    }

    // ==================================================================================
    // 3. Scale Factor Infinity Bounds & Coordinate Mapping Stress Tests
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-999.0)]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    public void ScaleFactor_InfinityAndBoundaryBounds_RdpControlTranslateCoordinatesDoesNotThrowOrReturnNaN(double scaleFactor)
    {
        var control = new RdpControl
        {
            Width = 1920,
            Height = 1080,
            ScaleFactor = scaleFactor
        };

        control.InitFrameBuffer(1920, 1080);

        // Test normal input point (960, 540)
        var inputPoint = new Point(960, 540);
        
        var ex = Record.Exception(() =>
        {
            control.TranslateCoordinates(inputPoint, out ushort x, out ushort y);
            Assert.True(x <= 1919, $"X coordinate {x} out of bounds for frame width 1920");
            Assert.True(y <= 1079, $"Y coordinate {y} out of bounds for frame height 1080");
        });

        Assert.Null(ex);
    }

    [AvaloniaFact]
    public void ScaleFactor_ExtremeControlPointInputs_HandledSafely()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);

        // Test extreme inputs to TranslateCoordinates
        var pointsToTest = new[]
        {
            new Point(double.PositiveInfinity, double.PositiveInfinity),
            new Point(double.NegativeInfinity, double.NegativeInfinity),
            new Point(double.NaN, double.NaN),
            new Point(double.MaxValue, double.MinValue),
            new Point(-100000, 100000)
        };

        foreach (var p in pointsToTest)
        {
            control.TranslateCoordinates(p, out ushort xPos, out ushort yPos);
            Assert.True(xPos < 1280, $"xPos {xPos} must be < 1280 for point ({p.X}, {p.Y})");
            Assert.True(yPos < 720, $"yPos {yPos} must be < 720 for point ({p.X}, {p.Y})");
        }
    }

    // ==================================================================================
    // 4. Profile Import/Export Encryption & Security Empirical Challenges
    // ==================================================================================

    [AvaloniaFact]
    public void CredentialProtection_PasswordStartingWithENC_BypassesEncryption()
    {
        // Vulnerability Hypothesis:
        // CredentialProtectionService.Protect checks plainText.StartsWith("ENC:").
        // If user's raw password starts with "ENC:", it returns plainText directly without encrypting!
        var service = new CredentialProtectionService();
        string plainPasswordWithPrefix = "ENC:UserSecretPassword123";

        string protectedResult = service.Protect(plainPasswordWithPrefix);

        // Check if returned string is identical unencrypted plaintext
        bool bypassedEncryption = (protectedResult == plainPasswordWithPrefix);

        Assert.False(bypassedEncryption, "SECURITY VULNERABILITY: CredentialProtectionService.Protect() returns passwords starting with 'ENC:' as unencrypted plain text.");
    }

    [AvaloniaFact]
    public void CredentialProtection_XorEncryption_TriviallyReversibleWithKnownKey()
    {
        var service = new CredentialProtectionService();
        string secret = "MySuperSecretPassword2026!";

        string protectedText = service.Protect(secret);
        Assert.StartsWith("ENC:", protectedText);

        // Demonstrate key weakness: static XOR key "CDP_WindowsRdpApp_SecretKey_2026"
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes("CDP_WindowsRdpApp_SecretKey_2026");
        string base64Payload = protectedText.Substring(4);
        byte[] cipherBytes = Convert.FromBase64String(base64Payload);

        byte[] recoveredBytes = new byte[cipherBytes.Length];
        for (int i = 0; i < cipherBytes.Length; i++)
        {
            recoveredBytes[i] = (byte)(cipherBytes[i] ^ keyBytes[i % keyBytes.Length]);
        }
        string manuallyDecrypted = System.Text.Encoding.UTF8.GetString(recoveredBytes);

        Assert.Equal(secret, manuallyDecrypted);
    }

    [AvaloniaFact]
    public async Task ProfileImportExport_RoundtripWithSpecialCharsAndCorruptedInputs()
    {
        string exportFile = Path.Combine(_tempTestDir, "export_roundtrip.json");
        string storageFile = Path.Combine(_tempTestDir, "storage.json");
        File.WriteAllText(storageFile, "[]");

        var storage = new ProfileStorageService(storageFile);
        var protection = new CredentialProtectionService();
        var vm = new ProfilesViewModel(storage, protection);
        await vm.LoadProfilesAsync();

        vm.NewName = "Export Test Server";
        vm.NewHost = "10.0.0.50";
        vm.NewPassword = "PassKey!@#$%^&*()_+-=[]{}|;':\",./<>?";
        vm.AddProfileCommand.Execute(null);

        // Export profiles
        await vm.ExecuteExportProfilesAsync(exportFile);
        Assert.True(File.Exists(exportFile));

        // Create new VM and import exported file
        var vm2 = new ProfilesViewModel(storage, protection);
        await vm2.ExecuteImportProfilesAsync(exportFile);

        var imported = vm2.Profiles.FirstOrDefault(p => p.Name == "Export Test Server");
        Assert.NotNull(imported);
        Assert.Equal("PassKey!@#$%^&*()_+-=[]{}|;':\",./<>?", imported.Password);
    }

    [AvaloniaFact]
    public async Task ProfileImport_CorruptedEncryptedPassword_HandledGracefullyWithoutCrash()
    {
        string corruptedImportFile = Path.Combine(_tempTestDir, "corrupted_import.json");
        string json = @"[
          {
            ""Id"": ""corrupt-1"",
            ""Name"": ""Corrupted Profile"",
            ""Host"": ""10.0.0.1"",
            ""Password"": ""ENC:DefinitelyNotValidBase64!!!"",
            ""Port"": 3389
          }
        ]";
        await File.WriteAllTextAsync(corruptedImportFile, json);

        string storageFile = Path.Combine(_tempTestDir, "storage_corrupt.json");
        File.WriteAllText(storageFile, "[]");

        var storage = new ProfileStorageService(storageFile);
        var protection = new CredentialProtectionService();
        var vm = new ProfilesViewModel(storage, protection);

        var ex = await Record.ExceptionAsync(() => vm.ExecuteImportProfilesAsync(corruptedImportFile));
        Assert.Null(ex);

        var corruptProfile = vm.Profiles.FirstOrDefault(p => p.Id == "corrupt-1");
        Assert.NotNull(corruptProfile);
        // Unprotect falls back to original string when base64 decode fails
        Assert.Equal("ENC:DefinitelyNotValidBase64!!!", corruptProfile.Password);
    }
}

public class DummyTestRdpSession : IRdpSession, IDisposable
{
    public RdpConnectionState State { get; private set; } = RdpConnectionState.Disconnected;
    public RdpSessionOptions Options => new RdpSessionOptions();
    public StaticVirtualChannelManager? StaticVirtualChannels => null;
    public DynamicVirtualChannelManager? DynamicVirtualChannels => null;

    public bool IsDisposed { get; private set; }
    public bool IsDisconnected { get; private set; }

    public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;
    public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        State = RdpConnectionState.Connected;
        StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(RdpConnectionState.Disconnected, RdpConnectionState.Connected));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsDisconnected = true;
        State = RdpConnectionState.Disconnected;
        StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(RdpConnectionState.Connected, RdpConnectionState.Disconnected));
        return Task.CompletedTask;
    }

    public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void RaiseStateChanged(RdpConnectionState newState)
    {
        var old = State;
        State = newState;
        StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(old, newState));
    }

    public void RaiseFrameUpdated()
    {
        FrameUpdated?.Invoke(this, new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>()));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        IsDisposed = true;
    }
}
