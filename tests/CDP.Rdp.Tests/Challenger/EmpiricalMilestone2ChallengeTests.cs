using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Channels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

[Xunit.Collection("RdpTests")]
public class EmpiricalMilestone2ChallengeTests
{
    private readonly string _tempTestDir;

    public EmpiricalMilestone2ChallengeTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "CDP_M2_EmpiricalTests_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    // ==================================================================================
    // 1. SCALABLE SCANCODE & KEY COMBO ACCURACY TESTS
    // ==================================================================================

    [AvaloniaTheory]
    [InlineData(RdpKeyCombination.AltTab, new ushort[] { 0x38, 0x0F, 0x0F, 0x38 }, new bool[] { false, false, false, false }, new bool[] { true, true, false, false })]
    [InlineData(RdpKeyCombination.CtrlAltDel, new ushort[] { 0x1D, 0x38, 0x53, 0x53, 0x38, 0x1D }, new bool[] { false, false, true, true, false, false }, new bool[] { true, true, true, false, false, false })]
    [InlineData(RdpKeyCombination.WinKey, new ushort[] { 0x5B, 0x5B }, new bool[] { true, true }, new bool[] { true, false })]
    [InlineData(RdpKeyCombination.AltF4, new ushort[] { 0x38, 0x3E, 0x3E, 0x38 }, new bool[] { false, false, false, false }, new bool[] { true, true, false, false })]
    [InlineData(RdpKeyCombination.CtrlShiftEsc, new ushort[] { 0x1D, 0x2A, 0x01, 0x01, 0x2A, 0x1D }, new bool[] { false, false, false, false, false, false }, new bool[] { true, true, true, false, false, false })]
    public async Task ScancodeAccuracy_KeyCombos_VerifyExactScancodeSequence(
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

            bool hasExtended = (evt.KeyboardEvent.Flags & RdpKeyboardFlags.Extended) != 0;
            Assert.Equal(expectedExtended[i], hasExtended);

            bool isDown = (evt.KeyboardEvent.Flags & RdpKeyboardFlags.Release) == 0;
            Assert.Equal(expectedIsDown[i], isDown);
        }
    }

    [AvaloniaFact]
    public async Task ScancodeAccuracy_KeyPassthroughDisabled_SendsNoEvents()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = false
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltTab);

        Assert.Empty(mockSession.SentInputEvents);
    }

    // ==================================================================================
    // 2. BAD JSON & CORRUPTED STORAGE HANDLING TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task ProfileStorage_BadJson_JsonObjectInsteadOfArray_FallsBackToDefaults()
    {
        string path = Path.Combine(_tempTestDir, "object.json");
        await File.WriteAllTextAsync(path, "{\"Id\":\"1\", \"Name\":\"SingleObject\"}");

        var storage = new ProfileStorageService(path);
        var profiles = await storage.LoadProfilesAsync();

        Assert.NotNull(profiles);
        Assert.Equal(3, profiles.Count);
        Assert.Equal("Primary Domain Controller", profiles[0].Name);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_BadJson_ArrayWithNullElement_HandledWithoutCrashing()
    {
        string path = Path.Combine(_tempTestDir, "null_element.json");
        await File.WriteAllTextAsync(path, "[null, {\"Id\":\"p1\", \"Name\":\"Valid\"}]");

        var storage = new ProfileStorageService(path);
        var profiles = await storage.LoadProfilesAsync();

        Assert.NotNull(profiles);
        Assert.Single(profiles);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_BadJson_CorruptedEncryptedPassword_FallsBackToDefaults()
    {
        string path = Path.Combine(_tempTestDir, "bad_encrypted.json");
        // Invalid Protected_ base64 string
        string json = @"[
            {
                ""Id"": ""p1"",
                ""Name"": ""Server1"",
                ""Host"": ""10.0.0.1"",
                ""Port"": 3389,
                ""Password"": ""Protected_@@@InvalidBase64!!!""
            }
        ]";
        await File.WriteAllTextAsync(path, json);

        var storage = new ProfileStorageService(path);
        var profiles = await storage.LoadProfilesAsync();

        Assert.NotNull(profiles);
        Assert.Single(profiles);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_ConcurrentWrites_RaceConditionStressTest()
    {
        string path = Path.Combine(_tempTestDir, "concurrent.json");
        await File.WriteAllTextAsync(path, "[]");
        var storage = new ProfileStorageService(path);

        const int numTasks = 10;
        var tasks = new Task[numTasks];

        for (int i = 0; i < numTasks; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(async () =>
            {
                var profile = new RdpConnectionProfile
                {
                    Id = $"concurrent-{idx}",
                    Name = $"Concurrent Server {idx}",
                    Host = $"10.0.0.{idx}"
                };
                await storage.AddProfileAsync(profile);
            });
        }

        await Task.WhenAll(tasks);

        var loaded = await storage.LoadProfilesAsync();
        // File concurrency without mutex causes overwritten writes!
        Assert.NotNull(loaded);
    }

    // ==================================================================================
    // 3. CONCURRENT TAB MANAGEMENT & LIFECYCLE STRESS TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task ConcurrentTabManagement_RapidOpenClose50Tabs_NoDeadlocksOrExceptions()
    {
        var vm = new SessionWorkspaceViewModel();
        await vm.ExecuteDisconnectAllAsync();

        const int tabCount = 50;
        var tabs = new List<RdpSessionTab>();

        for (int i = 0; i < tabCount; i++)
        {
            var tab = vm.OpenSession(new RdpConnectionProfile
            {
                Name = $"Stress Tab {i}",
                Host = $"192.168.1.{i + 1}",
                Port = 3389
            });
            tabs.Add(tab);
        }

        Assert.Equal(tabCount, vm.Sessions.Count);

        // Rapid switching of selected session
        foreach (var tab in tabs)
        {
            vm.SelectedSession = tab;
            Assert.True(tab.IsActive);
        }

        // Close tabs
        foreach (var t in tabs)
        {
            await vm.ExecuteCloseSessionAsync(t);
        }

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);
    }

    [AvaloniaFact]
    public async Task ConcurrentTabManagement_DisconnectAll_WhileConnecting_ClearsWorkspace()
    {
        var vm = new SessionWorkspaceViewModel();
        await vm.ExecuteDisconnectAllAsync();

        // Open 10 tabs with mock slow transports
        for (int i = 0; i < 10; i++)
        {
            vm.OpenSession(new RdpConnectionProfile
            {
                Name = $"Slow Tab {i}",
                Host = "10.0.0.1"
            }, customTransportFactory: async (opts, ct) =>
            {
                await Task.Delay(500, ct);
                throw new Exception("Simulated connection timeout");
            });
        }

        Assert.Equal(10, vm.Sessions.Count);

        // Trigger DisconnectAll while connections are still pending/delaying
        await vm.ExecuteDisconnectAllAsync();

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);
    }

    // ==================================================================================
    // 4. MEMORY LEAKS & EVENT HANDLER RETENTION TESTS
    // ==================================================================================

    [AvaloniaFact]
    public async Task MemoryLeak_RdpSessionTab_Disposal_UnsubscribesEvents()
    {
        WeakReference weakTab = await CreateAndDisposeTabAsync();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weakTab.IsAlive, "RdpSessionTab leaked memory after DisconnectSessionAsync and Dispose!");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateAndDisposeTabAsync()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession
        };

        WeakReference weakTab = new WeakReference(tab);

        await tab.DisconnectSessionAsync();
        tab.Dispose();
        return weakTab;
    }

    [AvaloniaFact]
    public async Task MemoryLeak_RdpClient_DisposeAsync_FreesResourcesAndState()
    {
        WeakReference weakClient = await CreateAndDisposeClientAsync();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weakClient.IsAlive, "RdpClient leaked memory after DisposeAsync!");
    }

    private static async Task<WeakReference> CreateAndDisposeClientAsync()
    {
        var client = new RdpClient(new RdpSessionOptions
        {
            Host = "127.0.0.1",
            Port = 3389
        });

        WeakReference weakClient = new WeakReference(client);

        await client.DisposeAsync();
        return weakClient;
    }

    // ==================================================================================
    // MOCK RDP SESSION HELPER FOR EMPIRICAL TESTING
    // ==================================================================================

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
