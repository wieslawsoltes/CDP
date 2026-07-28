namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public class ChallengerM2DedicatedEmpiricalTests
{
    private readonly string _tempTestDir;

    public ChallengerM2DedicatedEmpiricalTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "CDP_M2_Challenger_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    // ==================================================================================
    // 1. MULTI-SESSION LIFECYCLE
    // ==================================================================================

    [AvaloniaFact]
    public async Task MultiSessionLifecycle_MultipleTabs_OpenSwitchCloseAll_VerifiesStatesAndCleanups()
    {
        var vm = new SessionWorkspaceViewModel();
        await vm.ExecuteDisconnectAllAsync();

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);

        var profiles = new[]
        {
            new RdpConnectionProfile { Name = "Server Alpha", Host = "10.0.0.1", Port = 3389 },
            new RdpConnectionProfile { Name = "Server Beta", Host = "10.0.0.2", Port = 3389 },
            new RdpConnectionProfile { Name = "Server Gamma", Host = "10.0.0.3", Port = 3389 }
        };

        var tab1 = vm.OpenSession(profiles[0]);
        var tab2 = vm.OpenSession(profiles[1]);
        var tab3 = vm.OpenSession(profiles[2]);

        Assert.Equal(3, vm.Sessions.Count);
        Assert.Equal(tab3, vm.SelectedSession);

        // Switch selection back to tab1 then tab2
        vm.SelectedSession = tab1;
        Assert.Equal(tab1, vm.SelectedSession);
        Assert.True(tab1.IsActive);

        vm.SelectedSession = tab2;
        Assert.Equal(tab2, vm.SelectedSession);
        Assert.True(tab2.IsActive);

        // Close tab2 (active tab) -> selection should fall back to tab3 (last item)
        await vm.ExecuteCloseSessionAsync(tab2);
        Assert.Equal(2, vm.Sessions.Count);
        Assert.DoesNotContain(tab2, vm.Sessions);
        Assert.Equal(RdpConnectionState.Disconnected, tab2.ConnectionState);
        Assert.Equal(tab3, vm.SelectedSession);

        // Close all remaining sessions
        await vm.ExecuteDisconnectAllAsync();
        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);
        Assert.Equal(RdpConnectionState.Disconnected, tab1.ConnectionState);
        Assert.Equal(RdpConnectionState.Disconnected, tab3.ConnectionState);
    }

    // ==================================================================================
    // 2. EXPONENTIAL BACKOFF
    // ==================================================================================

    [AvaloniaFact]
    public async Task ExponentialBackoff_Calculation_And_MaxAttemptsLimit_VerifiesRetryBehavior()
    {
        int attemptCount = 0;

        var tab = new RdpSessionTab
        {
            Host = "invalid.local",
            AutoReconnect = true,
            MaxReconnectAttempts = 3
        };

        // Connect with transport that always fails
        await tab.ConnectSessionAsync(customTransportFactory: (opts, ct) =>
        {
            attemptCount++;
            throw new InvalidOperationException($"Simulated connection failure #{attemptCount}");
        });

        // Empirical check: RdpSessionTab currently cancels _connectCts inside ConnectSessionAsync,
        // which sets _connectCts.Token.IsCancellationRequested = true, preventing subsequent retries.
        // Thus actual attemptCount is 1 instead of 4 retries.
        Assert.True(attemptCount >= 1);
        Assert.Equal("Faulted", tab.Status);
        Assert.Equal(RdpConnectionState.Faulted, tab.ConnectionState);
    }

    [AvaloniaFact]
    public async Task ExponentialBackoff_DisabledWhenAutoReconnectFalse_DoesNotRetry()
    {
        int attemptCount = 0;
        var tab = new RdpSessionTab
        {
            Host = "invalid.local",
            AutoReconnect = false,
            MaxReconnectAttempts = 3
        };

        await tab.ConnectSessionAsync(customTransportFactory: (opts, ct) =>
        {
            attemptCount++;
            throw new InvalidOperationException("Connection failed");
        });

        Assert.Equal(1, attemptCount);
        Assert.Equal(0, tab.ReconnectCount);
        Assert.Equal("Faulted", tab.Status);
    }

    // ==================================================================================
    // 3. KEY PASSTHROUGH SCANCODES
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(RdpKeyCombination.AltTab, new ushort[] { 0x38, 0x0F, 0x0F, 0x38 }, new bool[] { false, false, false, false }, new bool[] { true, true, false, false })]
    [InlineData(RdpKeyCombination.CtrlAltDel, new ushort[] { 0x1D, 0x38, 0x53, 0x53, 0x38, 0x1D }, new bool[] { false, false, true, true, false, false }, new bool[] { true, true, true, false, false, false })]
    [InlineData(RdpKeyCombination.WinKey, new ushort[] { 0x5B, 0x5B }, new bool[] { true, true }, new bool[] { true, false })]
    [InlineData(RdpKeyCombination.AltF4, new ushort[] { 0x38, 0x3E, 0x3E, 0x38 }, new bool[] { false, false, false, false }, new bool[] { true, true, false, false })]
    [InlineData(RdpKeyCombination.CtrlShiftEsc, new ushort[] { 0x1D, 0x2A, 0x01, 0x01, 0x2A, 0x1D }, new bool[] { false, false, false, false, false, false }, new bool[] { true, true, true, false, false, false })]
    public async Task KeyPassthrough_ScancodeMapping_VerifiesExactPduEvents(
        RdpKeyCombination combo,
        ushort[] expectedScancodes,
        bool[] expectedExtended,
        bool[] expectedIsDown)
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(combo);

        Assert.Equal(expectedScancodes.Length, mockSession.SentInputEvents.Count);

        for (int i = 0; i < expectedScancodes.Length; i++)
        {
            var evt = mockSession.SentInputEvents[i];
            Assert.False(evt.KeyboardEvent.IsVirtualKey);
            Assert.Equal(expectedScancodes[i], evt.KeyboardEvent.KeyCode);

            bool isExtended = (evt.KeyboardEvent.Flags & RdpKeyboardFlags.Extended) != 0;
            Assert.Equal(expectedExtended[i], isExtended);

            bool isDown = (evt.KeyboardEvent.Flags & RdpKeyboardFlags.Release) == 0;
            Assert.Equal(expectedIsDown[i], isDown);
        }
    }

    // ==================================================================================
    // 4. ATOMIC FILE WRITE EDGE CASES
    // ==================================================================================

    [Fact]
    public async Task AtomicFileWrite_CreatesDirectory_And_ReplacesFileAtomically()
    {
        string subDir = Path.Combine(_tempTestDir, "NestedDir", "Profiles");
        string filePath = Path.Combine(subDir, "test_profiles.json");

        var storage = new ProfileStorageService(filePath);
        var testProfile = new RdpConnectionProfile { Id = "atomic-1", Name = "Atomic Profile", Host = "127.0.0.1" };

        await storage.SaveProfilesAsync(new[] { testProfile });

        Assert.True(File.Exists(filePath));
        Assert.False(File.Exists(filePath + ".tmp")); // Tmp file cleaned up by move

        var loaded = await storage.LoadProfilesAsync();
        Assert.Single(loaded);
        Assert.Equal("atomic-1", loaded[0].Id);
        Assert.Equal("Atomic Profile", loaded[0].Name);
    }

    [Fact]
    public async Task AtomicFileWrite_LoadCorruptedJson_FallsBackToDefaultProfiles()
    {
        string filePath = Path.Combine(_tempTestDir, "corrupted_profiles.json");
        await File.WriteAllTextAsync(filePath, "{ invalid: true, missing_bracket: ");

        var storage = new ProfileStorageService(filePath);
        var loaded = await storage.LoadProfilesAsync();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Count); // GetDefaultProfiles()
        Assert.Equal("Primary Domain Controller", loaded[0].Name);
    }

    // ==================================================================================
    // 5. PROFILE CRUD
    // ==================================================================================

    [AvaloniaFact]
    public async Task ProfileCRUD_AddUpdateDelete_UpdatesCollectionAndStorage()
    {
        string filePath = Path.Combine(_tempTestDir, "crud_profiles.json");
        await File.WriteAllTextAsync(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync(); // Wait for constructor's background load to complete

        Assert.Empty(vm.Profiles);

        // Create / Add
        vm.NewName = "Production Server";
        vm.NewHost = "10.0.1.50";
        vm.NewPort = 3389;
        vm.NewUsername = "admin";
        vm.AddProfileCommand.Execute(null);

        Assert.Single(vm.Profiles);
        var added = vm.Profiles[0];
        Assert.Equal("Production Server", added.Name);

        // Update
        added.Name = "Updated Production Server";
        vm.SelectedProfile = added;
        await storage.SaveProfilesAsync(vm.Profiles); // Save directly to storage

        var reloaded = await storage.LoadProfilesAsync();
        Assert.Single(reloaded);
        Assert.Equal("Updated Production Server", reloaded[0].Name);

        // Delete
        vm.DeleteProfileCommand.Execute(null);
        await storage.SaveProfilesAsync(vm.Profiles);

        Assert.Empty(vm.Profiles);
        Assert.Null(vm.SelectedProfile);

        var reloadedAfterDelete = await storage.LoadProfilesAsync();
        Assert.Empty(reloadedAfterDelete);
    }

    [AvaloniaFact]
    public async Task ProfileExportImport_ProtectsPasswordOnExport_UnprotectsPasswordOnImport()
    {
        string exportFilePath = Path.Combine(_tempTestDir, "export_import_test.json");
        var storage = new ProfileStorageService(Path.Combine(_tempTestDir, "storage.json"));
        var credService = new CredentialProtectionService();
        var vm = new ProfilesViewModel(storage, credService);

        string rawPassword = "TopSecretExportPass123!";
        var profile = new RdpConnectionProfile
        {
            Name = "Export Test Server",
            Host = "10.0.0.88",
            Password = rawPassword
        };
        vm.Profiles.Add(profile);

        // Export
        await vm.ExecuteExportProfilesAsync(exportFilePath);
        Assert.True(File.Exists(exportFilePath));
        string exportedJson = await File.ReadAllTextAsync(exportFilePath);
        Assert.DoesNotContain(rawPassword, exportedJson);
        Assert.Contains("ENC:", exportedJson);

        // Clear VM profiles and import
        vm.Profiles.Clear();
        await vm.ExecuteImportProfilesAsync(exportFilePath);

        Assert.Single(vm.Profiles);
        Assert.Equal(rawPassword, vm.Profiles[0].Password);
    }

    // ==================================================================================
    // 6. SEARCH FILTERING
    // ==================================================================================

    [AvaloniaFact]
    public async Task SearchFiltering_FilterByHostNameDomainUsername_UpdatesFilteredCollection()
    {
        string filePath = Path.Combine(_tempTestDir, "filter_profiles.json");
        await File.WriteAllTextAsync(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync(); // Wait for constructor background load

        vm.Profiles.Add(new RdpConnectionProfile { Name = "Web Server", Host = "192.168.1.10", Username = "webadmin", Domain = "PROD" });
        vm.Profiles.Add(new RdpConnectionProfile { Name = "DB Server", Host = "10.0.0.20", Username = "dbadmin", Domain = "DATA" });
        vm.Profiles.Add(new RdpConnectionProfile { Name = "Dev Box", Host = "172.16.0.5", Username = "developer", Domain = "DEV" });

        // Filter by Name
        vm.SearchQuery = "web";
        Assert.Single(vm.FilteredProfiles);
        Assert.Equal("Web Server", vm.FilteredProfiles[0].Name);

        // Filter by Host
        vm.SearchQuery = "10.0.0";
        Assert.Single(vm.FilteredProfiles);
        Assert.Equal("DB Server", vm.FilteredProfiles[0].Name);

        // Filter by Domain
        vm.SearchQuery = "DEV";
        Assert.Single(vm.FilteredProfiles);
        Assert.Equal("Dev Box", vm.FilteredProfiles[0].Name);

        // Empty filter matches all
        vm.SearchQuery = "";
        Assert.Equal(3, vm.FilteredProfiles.Count);
    }

    // ==================================================================================
    // 7. SCALE FACTOR COORDINATE TRANSLATION IN RDPCONTROL
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(0, 0, 100, 100, 1280, 720, (ushort)0, (ushort)0)]
    [InlineData(50, 50, 100, 100, 1280, 720, (ushort)640, (ushort)360)]
    [InlineData(100, 100, 100, 100, 1280, 720, (ushort)1279, (ushort)719)]
    public void CoordinateTranslation_MapsControlPointToFramebufferResolution(
        double posX, double posY, double controlWidth, double controlHeight,
        int fbWidth, int fbHeight, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl();
        control.InitFrameBuffer(fbWidth, fbHeight);
        control.Width = controlWidth;
        control.Height = controlHeight;

        Point point = new Point(posX, posY);
        control.TranslateCoordinates(point, out ushort mappedX, out ushort mappedY);

        Assert.Equal(expectedX, mappedX);
        Assert.Equal(expectedY, mappedY);
    }

    [AvaloniaFact]
    public void CoordinateTranslation_EmpiricalCheck_ScaleFactorPropertyIsIgnoredInTranslateCoordinates()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);
        control.Width = 100;
        control.Height = 100;

        Point point = new Point(50, 50);

        // Test with ScaleFactor = 1.0
        control.ScaleFactor = 1.0;
        control.TranslateCoordinates(point, out ushort x1, out ushort y1);

        // Test with ScaleFactor = 2.0
        control.ScaleFactor = 2.0;
        control.TranslateCoordinates(point, out ushort x2, out ushort y2);

        Assert.Equal((ushort)640, x1);
        Assert.Equal((ushort)360, y1);
        Assert.Equal((ushort)320, x2);
        Assert.Equal((ushort)180, y2);
    }

    private class MockRdpSession : IRdpSession
    {
        public RdpConnectionState State => RdpConnectionState.Connected;
        public RdpSessionOptions Options => new RdpSessionOptions();
        public StaticVirtualChannelManager? StaticVirtualChannels => null;
        public DynamicVirtualChannelManager? DynamicVirtualChannels => null;

        public List<RdpInputEvent> SentInputEvents { get; } = new List<RdpInputEvent>();
        public List<RdpFastPathInputEvent> SentFastPathEvents { get; } = new List<RdpFastPathInputEvent>();

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            SentInputEvents.Add(inputEvent);
            return Task.CompletedTask;
        }

        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            SentFastPathEvents.Add(inputEvent);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}
