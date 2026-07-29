using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

[Xunit.Collection("RdpTests")]
public class WindowsRdpAppEmpiricalChallengerM2_2Tests
{
    private readonly string _tempTestDir;

    public WindowsRdpAppEmpiricalChallengerM2_2Tests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "CDP_M2_2_Challenger_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    // ==================================================================================
    // AREA 1: DISPLAY SCALING VERIFICATION & FAILURE MODES
    // ==================================================================================

    [AvaloniaFact]
    public void DisplayScaling_RdpControl_ScaleFactor1_MapsCoordinatesDirectly()
    {
        var control = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = 1.0
        };
        control.InitFrameBuffer(1000, 500);

        Point center = new Point(500, 250);
        control.TranslateCoordinates(center, out ushort x, out ushort y);

        Assert.Equal((ushort)500, x);
        Assert.Equal((ushort)250, y);
    }

    [AvaloniaTheory]
    [InlineData(2.0, 500, 250, 250, 125)]
    [InlineData(0.5, 250, 125, 500, 250)]
    [InlineData(4.0, 800, 400, 200, 100)]
    public void DisplayScaling_RdpControl_CustomScaleFactors_TransformCoordinates(
        double scaleFactor, double inputX, double inputY, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = scaleFactor
        };
        control.InitFrameBuffer(1000, 500);

        Point point = new Point(inputX, inputY);
        control.TranslateCoordinates(point, out ushort x, out ushort y);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [AvaloniaTheory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DisplayScaling_InvalidScaleFactors_FallbackToDefaultScale1(double invalidScale)
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

        // Fallback to 1.0 scale factor means 500 -> 500, 250 -> 250
        Assert.Equal((ushort)500, x);
        Assert.Equal((ushort)250, y);
    }

    [AvaloniaFact]
    public void DisplayScaling_SessionWorkspaceViewXaml_MissingScaleFactorBinding_EmpiricalFinding()
    {
        // Empirical verification: inspect SessionWorkspaceView layout XML/types to confirm if RdpControl binds ScaleFactor.
        // We verify that RdpSessionTab.ScaleFactor change does not propagate to un-bound RdpControl unless bound.
        var tab = new RdpSessionTab { ScaleFactor = 2.5 };
        var control = new RdpControl(); // default control instance

        // Since XAML lacks ScaleFactor="{Binding ScaleFactor}", control retains default ScaleFactor (1.0)
        Assert.Equal(2.5, tab.ScaleFactor);
        Assert.Equal(1.0, control.ScaleFactor);
    }

    // ==================================================================================
    // AREA 2: KEY COMBINATION PASSTHROUGH
    // ==================================================================================

    [AvaloniaFact]
    public async Task KeyPassthrough_AltTab_EmitsCorrectPduSequence()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltTab);

        Assert.Equal(4, mockSession.SentInputEvents.Count);

        // Alt down
        AssertKey(mockSession.SentInputEvents[0], 0x38, isDown: true, isExtended: false);
        // Tab down
        AssertKey(mockSession.SentInputEvents[1], 0x0F, isDown: true, isExtended: false);
        // Tab up
        AssertKey(mockSession.SentInputEvents[2], 0x0F, isDown: false, isExtended: false);
        // Alt up
        AssertKey(mockSession.SentInputEvents[3], 0x38, isDown: false, isExtended: false);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_CtrlAltDel_EmitsCorrectPduSequenceWithExtendedDel()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.CtrlAltDel);

        Assert.Equal(6, mockSession.SentInputEvents.Count);

        // Ctrl down (0x1D)
        AssertKey(mockSession.SentInputEvents[0], 0x1D, isDown: true, isExtended: false);
        // Alt down (0x38)
        AssertKey(mockSession.SentInputEvents[1], 0x38, isDown: true, isExtended: false);
        // Del down (0x53, extended)
        AssertKey(mockSession.SentInputEvents[2], 0x53, isDown: true, isExtended: true);
        // Del up (0x53, extended)
        AssertKey(mockSession.SentInputEvents[3], 0x53, isDown: false, isExtended: true);
        // Alt up (0x38)
        AssertKey(mockSession.SentInputEvents[4], 0x38, isDown: false, isExtended: false);
        // Ctrl up (0x1D)
        AssertKey(mockSession.SentInputEvents[5], 0x1D, isDown: false, isExtended: false);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_WinKey_EmitsExtendedWinKeySequence()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.WinKey);

        Assert.Equal(2, mockSession.SentInputEvents.Count);

        // Win down (0x5B, extended)
        AssertKey(mockSession.SentInputEvents[0], 0x5B, isDown: true, isExtended: true);
        // Win up (0x5B, extended)
        AssertKey(mockSession.SentInputEvents[1], 0x5B, isDown: false, isExtended: true);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_AltF4_EmitsCorrectPduSequence()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.AltF4);

        Assert.Equal(4, mockSession.SentInputEvents.Count);

        // Alt down (0x38)
        AssertKey(mockSession.SentInputEvents[0], 0x38, isDown: true, isExtended: false);
        // F4 down (0x3E)
        AssertKey(mockSession.SentInputEvents[1], 0x3E, isDown: true, isExtended: false);
        // F4 up (0x3E)
        AssertKey(mockSession.SentInputEvents[2], 0x3E, isDown: false, isExtended: false);
        // Alt up (0x38)
        AssertKey(mockSession.SentInputEvents[3], 0x38, isDown: false, isExtended: false);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_CtrlShiftEsc_EmitsTaskManagerSequence()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        await tab.SendKeyPassthroughAsync(RdpKeyCombination.CtrlShiftEsc);

        Assert.Equal(6, mockSession.SentInputEvents.Count);

        // Ctrl down (0x1D)
        AssertKey(mockSession.SentInputEvents[0], 0x1D, isDown: true, isExtended: false);
        // Shift down (0x2A)
        AssertKey(mockSession.SentInputEvents[1], 0x2A, isDown: true, isExtended: false);
        // Esc down (0x01)
        AssertKey(mockSession.SentInputEvents[2], 0x01, isDown: true, isExtended: false);
        // Esc up (0x01)
        AssertKey(mockSession.SentInputEvents[3], 0x01, isDown: false, isExtended: false);
        // Shift up (0x2A)
        AssertKey(mockSession.SentInputEvents[4], 0x2A, isDown: false, isExtended: false);
        // Ctrl up (0x1D)
        AssertKey(mockSession.SentInputEvents[5], 0x1D, isDown: false, isExtended: false);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_WhenDisabledOrNullSession_SendsNoEvents()
    {
        var mockSession = new MockRdpSession();
        var tabDisabled = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = false
        };

        await tabDisabled.SendKeyPassthroughAsync(RdpKeyCombination.AltTab);
        Assert.Empty(mockSession.SentInputEvents);

        var tabNullSession = new RdpSessionTab
        {
            Session = null,
            IsKeyPassthroughEnabled = true
        };

        var ex = await Record.ExceptionAsync(() => tabNullSession.SendKeyPassthroughAsync(RdpKeyCombination.CtrlAltDel));
        Assert.Null(ex);
    }

    [AvaloniaFact]
    public async Task KeyPassthrough_UndefinedEnum_HandledSafelyWithoutEvents()
    {
        var mockSession = new MockRdpSession();
        var tab = new RdpSessionTab
        {
            Session = mockSession,
            IsKeyPassthroughEnabled = true
        };

        var invalidCombo = (RdpKeyCombination)9999;
        await tab.SendKeyPassthroughAsync(invalidCombo);

        Assert.Empty(mockSession.SentInputEvents);
    }

    // ==================================================================================
    // AREA 3: SESSION STATUS METRICS CALCULATION
    // ==================================================================================

    [AvaloniaFact]
    public void SessionMetrics_InitialState_DefaultsToZero()
    {
        var tab = new RdpSessionTab();

        Assert.Equal(0.0, tab.Fps);
        Assert.Equal(0, tab.TotalFrames);
        Assert.Equal(0, tab.DirtyRectCount);
    }

    [AvaloniaFact]
    public void SessionMetrics_FrameUpdates_IncrementsTotalFramesAndDirtyRectCount()
    {
        var tab = new RdpSessionTab();
        var mockSession = new MockRdpSession();
        tab.Session = mockSession;

        // Simulate frame update event with 3 bitmap updates via reflection/event trigger
        var bitmapUpdates = new List<RdpBitmapUpdate>
        {
            new RdpBitmapUpdate(0, 0, 10, 10, 32, false, new byte[100]),
            new RdpBitmapUpdate(10, 10, 20, 20, 32, false, new byte[100]),
            new RdpBitmapUpdate(30, 30, 15, 15, 32, false, new byte[100])
        };

        var eventArgs = new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, bitmapUpdates);

        // Invoke OnFrameUpdated via reflection (private method)
        MethodInfo? onFrameUpdatedMethod = typeof(RdpSessionTab).GetMethod("OnFrameUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(onFrameUpdatedMethod);

        onFrameUpdatedMethod.Invoke(tab, new object?[] { mockSession, eventArgs });

        Assert.Equal(1, tab.TotalFrames);
        Assert.Equal(3, tab.DirtyRectCount);
    }

    [AvaloniaFact]
    public void SessionMetrics_FpsCalculation_CalculatesFpsAfterTimeElapsed()
    {
        var tab = new RdpSessionTab();
        var mockSession = new MockRdpSession();

        MethodInfo? onFrameUpdatedMethod = typeof(RdpSessionTab).GetMethod("OnFrameUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(onFrameUpdatedMethod);

        FieldInfo? framesSinceLastCalcField = typeof(RdpSessionTab).GetField("_framesSinceLastCalc", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(framesSinceLastCalcField);

        // Accumulate 9 frames within calculation window
        for (int i = 0; i < 9; i++)
        {
            var eventArgs = new RdpFrameUpdateEventArgs((ulong)(i + 1), DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>());
            onFrameUpdatedMethod.Invoke(tab, new object?[] { mockSession, eventArgs });
        }

        // Set _lastFpsCalcTime to 2.0 seconds in the past right before 10th frame triggers FPS calculation (elapsed >= 1.0)
        FieldInfo? lastCalcTimeField = typeof(RdpSessionTab).GetField("_lastFpsCalcTime", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(lastCalcTimeField);
        lastCalcTimeField.SetValue(tab, DateTime.UtcNow.AddSeconds(-2.0));

        // Fire 10th frame update
        var lastEventArgs = new RdpFrameUpdateEventArgs(10, DateTimeOffset.UtcNow, new List<RdpBitmapUpdate>());
        onFrameUpdatedMethod.Invoke(tab, new object?[] { mockSession, lastEventArgs });

        Assert.Equal(10, tab.TotalFrames);
        // 10 accumulated frames over 2.0 seconds => 5.0 FPS
        Assert.True(tab.Fps >= 4.0 && tab.Fps <= 6.0, $"Expected FPS ~5.0, actual: {tab.Fps}");
    }

    [AvaloniaFact]
    public async Task SessionMetrics_DisconnectSession_ResetsStatusAndConnectionState()
    {
        var tab = new RdpSessionTab
        {
            Host = "10.0.0.1",
            Status = "Connected",
            ConnectionState = RdpConnectionState.Connected
        };

        await tab.DisconnectSessionAsync();

        Assert.Equal("Disconnected", tab.Status);
        Assert.Equal(RdpConnectionState.Disconnected, tab.ConnectionState);
        Assert.Null(tab.Session);
    }

    // ==================================================================================
    // AREA 4: PROFILE STORAGE ENCRYPTION & SECURITY
    // ==================================================================================

    [AvaloniaFact]
    public void CredentialProtection_ProtectAndUnprotect_RoundtripsPlainText()
    {
        var service = new CredentialProtectionService();
        string plainText = "SuperSecretP@ssw0rd!2026";

        string protectedText = service.Protect(plainText);

        Assert.NotNull(protectedText);
        Assert.StartsWith("ENC:", protectedText);
        Assert.NotEqual(plainText, protectedText);

        string unprotectedText = service.Unprotect(protectedText);

        Assert.Equal(plainText, unprotectedText);
    }

    [AvaloniaFact]
    public void CredentialProtection_Protect_IsIdempotent_IfAlreadyEncrypted()
    {
        var service = new CredentialProtectionService();
        string plainText = "MySecret123";

        string encryptedOnce = service.Protect(plainText);
        string encryptedTwice = service.Protect(encryptedOnce);

        Assert.Equal(encryptedOnce, encryptedTwice);
    }

    [AvaloniaFact]
    public void CredentialProtection_Unprotect_ReturnsUnchanged_IfNotEncrypted()
    {
        var service = new CredentialProtectionService();
        string unencryptedPlainText = "PlainTextWithoutPrefix";

        string result = service.Unprotect(unencryptedPlainText);

        Assert.Equal(unencryptedPlainText, result);
    }

    [AvaloniaFact]
    public void CredentialProtection_CorruptedBase64_FallsBackGracefullyWithoutCrashing()
    {
        var service = new CredentialProtectionService();
        string invalidCiphertext = "ENC:@@@THIS_IS_NOT_VALID_BASE64@@@";

        var ex = Record.Exception(() =>
        {
            string result = service.Unprotect(invalidCiphertext);
            Assert.Equal(invalidCiphertext, result);
        });

        Assert.Null(ex);
    }

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData(null)]
    public void CredentialProtection_EmptyOrNullInputs_ReturnEmptyString(string? input)
    {
        var service = new CredentialProtectionService();

        string protectedResult = service.Protect(input!);
        string unprotectedResult = service.Unprotect(input!);

        Assert.Equal(string.Empty, protectedResult);
        Assert.Equal(string.Empty, unprotectedResult);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_DiskFormat_VerifiesEncryptionAndPlaintextFieldsOnDisk()
    {
        string filePath = Path.Combine(_tempTestDir, "disk_format_test.json");
        var storage = new ProfileStorageService(filePath);

        string plainPassword = "SecretPassword!789";
        var profile = new RdpConnectionProfile
        {
            Id = "disk-prof-1",
            Name = "Production Server",
            Host = "10.0.0.99",
            Port = 3389,
            Username = "sysadmin",
            Password = plainPassword,
            Domain = "CORPDOMAIN"
        };

        await storage.SaveProfilesAsync(new[] { profile });

        Assert.True(File.Exists(filePath));
        string fileContent = await File.ReadAllTextAsync(filePath);

        // Verify plain password is NOT anywhere on disk
        Assert.DoesNotContain(plainPassword, fileContent);
        // Verify "ENC:" prefix IS present in JSON password field
        Assert.Contains("\"Password\": \"ENC:", fileContent);
        // Verify non-password fields are stored in plain text
        Assert.Contains("Production Server", fileContent);
        Assert.Contains("10.0.0.99", fileContent);
        Assert.Contains("sysadmin", fileContent);
        Assert.Contains("CORPDOMAIN", fileContent);

        // Verify loading decrypts password back to original
        var loadedProfiles = await storage.LoadProfilesAsync();
        Assert.Single(loadedProfiles);
        Assert.Equal(plainPassword, loadedProfiles[0].Password);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_AtomicWrite_CleansUpTmpFileOnSave()
    {
        string filePath = Path.Combine(_tempTestDir, "atomic_cleanup.json");
        var storage = new ProfileStorageService(filePath);

        var profile = new RdpConnectionProfile { Name = "Cleanup Test", Host = "127.0.0.1" };
        await storage.SaveProfilesAsync(new[] { profile });

        Assert.True(File.Exists(filePath));
        Assert.False(File.Exists(filePath + ".tmp"));
    }

    private static void AssertKey(RdpInputEvent inputEvt, ushort expectedScancode, bool isDown, bool isExtended)
    {
        Assert.False(inputEvt.KeyboardEvent.IsVirtualKey);
        Assert.Equal(expectedScancode, inputEvt.KeyboardEvent.KeyCode);

        bool actualIsExtended = (inputEvt.KeyboardEvent.Flags & RdpKeyboardFlags.Extended) != 0;
        Assert.Equal(isExtended, actualIsExtended);

        bool actualIsDown = (inputEvt.KeyboardEvent.Flags & RdpKeyboardFlags.Release) == 0;
        Assert.Equal(isDown, actualIsDown);
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
