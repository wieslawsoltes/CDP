namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Rendering;
using CDP.Rdp.Session;
using CdpRdpApp;
using CdpRdpApp.ViewModels;
using Xunit;

public class RdpEmpiricalChallengerM4Tests
{
    private class DummyRdpSession : IRdpSession
    {
        public RdpConnectionState State { get; set; } = RdpConnectionState.Connected;
        public RdpSessionOptions Options { get; } = new RdpSessionOptions();

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public List<RdpInputEvent> SentInputEvents { get; } = new();
        public List<RdpFastPathInputEvent> SentFastPathEvents { get; } = new();

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            lock (SentInputEvents)
            {
                SentInputEvents.Add(inputEvent);
            }
            return Task.CompletedTask;
        }

        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            lock (SentFastPathEvents)
            {
                SentFastPathEvents.Add(inputEvent);
            }
            return Task.CompletedTask;
        }

        public void RaiseFrameUpdated(RdpFrameUpdateEventArgs args)
        {
            FrameUpdated?.Invoke(this, args);
        }

        public void RaiseStateChanged(RdpConnectionStateChangedEventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public void CoordinateMapping_BoundaryAndOutofBounds_EmpiricalTest()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);
        control.Width = 640;
        control.Height = 360;

        // 1. Normal inside bounds
        control.TranslateCoordinates(new Point(320, 180), out ushort x1, out ushort y1);
        Assert.Equal((ushort)640, x1);
        Assert.Equal((ushort)360, y1);

        // 2. Negative coordinates
        control.TranslateCoordinates(new Point(-100, -50), out ushort xNeg, out ushort yNeg);
        Assert.Equal((ushort)0, xNeg);
        Assert.Equal((ushort)0, yNeg);

        // 3. Huge positive out-of-bounds
        control.TranslateCoordinates(new Point(99999, 88888), out ushort xHuge, out ushort yHuge);
        Assert.Equal((ushort)1279, xHuge);
        Assert.Equal((ushort)719, yHuge);

        // 4. Positive Infinity
        control.TranslateCoordinates(new Point(double.PositiveInfinity, double.PositiveInfinity), out ushort xInf, out ushort yInf);
        // Note: Check what PositiveInfinity evaluates to in TranslateCoordinates
        // If (int)double.PositiveInfinity produces int.MinValue (-2147483648), Math.Clamp clamps to 0!
        // Record actual behavior for analysis report:
        bool infinityMappedToZero = (xInf == 0 && yInf == 0);
        bool infinityMappedToMax = (xInf == 1279 && yInf == 719);
        Assert.True(infinityMappedToZero || infinityMappedToMax, $"PositiveInfinity mapped to x={xInf}, y={yInf}");

        // 5. NaN
        control.TranslateCoordinates(new Point(double.NaN, double.NaN), out ushort xNan, out ushort yNan);
        Assert.Equal((ushort)0, xNan);
        Assert.Equal((ushort)0, yNan);
    }

    [AvaloniaFact]
    public async Task DispatchMouseEvent_OutofBoundsAndInvalidValues_EmpiricalTest()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Negative coordinates
        var res1 = await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mousePressed",
            ["x"] = -50.0,
            ["y"] = -100.0,
            ["button"] = "left"
        });
        Assert.NotNull(res1);

        // Huge coordinates
        var res2 = await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseMoved",
            ["x"] = 999999.0,
            ["y"] = 888888.0,
            ["button"] = "none"
        });
        Assert.NotNull(res2);

        // Missing x and y (defaults to 0)
        var res3 = await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["button"] = "left"
        });
        Assert.NotNull(res3);

        window.Close();
    }

    [AvaloniaFact]
    public async Task EmptyString_TextInsertion_EmpiricalTest()
    {
        var window = new MainWindow();
        window.Show();

        var txtHost = window.FindControl<TextBox>("txtHost");
        Assert.NotNull(txtHost);
        txtHost.Text = "Initial";
        txtHost.Focus();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Empty string text insertion
        await InputDomain.HandleAsync(session, "insertText", new JsonObject
        {
            ["text"] = ""
        });
        Assert.Equal("Initial", txtHost.Text);

        // 2. Missing "text" property
        await InputDomain.HandleAsync(session, "insertText", new JsonObject());
        Assert.Equal("Initial", txtHost.Text);

        // 3. RdpControl OnTextInput empty string
        var control = new RdpControl();
        var dummySession = new DummyRdpSession();
        control.Session = dummySession;

        control.RaiseEvent(new TextInputEventArgs { Text = "", RoutedEvent = InputElement.TextInputEvent });
        Assert.Empty(dummySession.SentInputEvents);

        control.RaiseEvent(new TextInputEventArgs { Text = null, RoutedEvent = InputElement.TextInputEvent });
        Assert.Empty(dummySession.SentInputEvents);

        window.Close();
    }

    [Fact]
    public void UnicodeSymbols_SurrogatePairs_EmpiricalTest()
    {
        var control = new RdpControl();
        var dummySession = new DummyRdpSession();
        control.Session = dummySession;

        // 1. CJK & Accented text (BMP characters)
        string bmpText = "中文éñ";
        control.RaiseEvent(new TextInputEventArgs { Text = bmpText, RoutedEvent = InputElement.TextInputEvent });

        // 4 characters -> 8 events (Down + Release per char)
        Assert.Equal(8, dummySession.SentInputEvents.Count);
        Assert.Equal('中', dummySession.SentInputEvents[0].KeyboardEvent.KeyCode);
        Assert.Equal('文', dummySession.SentInputEvents[2].KeyboardEvent.KeyCode);
        Assert.Equal('é', dummySession.SentInputEvents[4].KeyboardEvent.KeyCode);
        Assert.Equal('ñ', dummySession.SentInputEvents[6].KeyboardEvent.KeyCode);

        dummySession.SentInputEvents.Clear();

        // 2. Emoji / Surrogate Pair character (e.g. "😀" \uD83D\uDE00)
        string emojiText = "😀";
        control.RaiseEvent(new TextInputEventArgs { Text = emojiText, RoutedEvent = InputElement.TextInputEvent });

        // Verification of surrogate pair handling:
        // High surrogate \uD83D (55357) and Low surrogate \uDE00 (56832)
        // If foreach (char ch in e.Text) is used, it emits 4 events (2 pairs of invalid surrogate chars)
        int eventCount = dummySession.SentInputEvents.Count;
        Assert.True(eventCount == 4 || eventCount == 2, $"Event count for emoji: {eventCount}");

        uint firstCode = dummySession.SentInputEvents[0].KeyboardEvent.KeyCode;
        bool isSplitSurrogate = firstCode <= 0xFFFF && char.IsHighSurrogate((char)firstCode);

        // Record surrogate pair behavior: if isSplitSurrogate is true, then surrogate pair is split into raw UTF-16 code units!
        Assert.True(isSplitSurrogate || firstCode == 0x1F600, $"Emoji key code: 0x{firstCode:X4}");
    }

    [Fact]
    public void RapidFrameUpdates_Backpressure_EmpiricalTest()
    {
        var control = new RdpControl();
        var dummySession = new DummyRdpSession();
        control.Session = dummySession;

        byte[] frameData = new byte[100 * 100 * 4];
        for (int i = 0; i < frameData.Length; i++) frameData[i] = (byte)(i % 256);

        var bmpUpdate = new RdpBitmapUpdate(0, 0, 100, 100, 32, false, frameData);
        var args = new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { bmpUpdate });

        var startTime = DateTime.UtcNow;
        int frameCount = 2000;

        for (int i = 0; i < frameCount; i++)
        {
            dummySession.RaiseFrameUpdated(args);
        }

        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Assert.NotNull(control.FrameBuffer);
        Assert.Equal(1280, control.FrameBuffer.Width);
        Assert.True(elapsedMs < 5000, $"2000 frame updates took {elapsedMs:F2}ms");
    }

    [AvaloniaFact]
    public async Task ConcurrentCdpSessions_LifecycleAndInteractions_EmpiricalTest()
    {
        var window = new MainWindow();
        window.Show();

        int sessionCount = 10;
        var sessions = new List<CdpSession>();
        var clientSockets = new List<ClientWebSocket>();

        for (int i = 0; i < sessionCount; i++)
        {
            var ws = new ClientWebSocket();
            clientSockets.Add(ws);
            var session = new CdpSession(ws, window);
            sessions.Add(session);
        }

        for (int i = 0; i < sessionCount; i++)
        {
            int idx = i;
            var session = sessions[idx];

            // Enable Input
            await InputDomain.HandleAsync(session, "enable", new JsonObject());

            // Get Document
            var doc = await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["depth"] = -1 });
            Assert.NotNull(doc);

            // Mouse Event
            await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = 10 * idx,
                ["y"] = 10 * idx
            });

            // Evaluate
            var eval = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
            {
                ["expression"] = "Window.DataContext.Host"
            });
            Assert.NotNull(eval);

            // Capture Screenshot
            var shot = await PageDomain.HandleAsync(session, "captureScreenshot", new JsonObject { ["format"] = "png" });
            Assert.NotNull(shot);

            // Disable Input
            await InputDomain.HandleAsync(session, "disable", new JsonObject());
        }

        foreach (var ws in clientSockets)
        {
            ws.Dispose();
        }

        window.Close();
    }

    [Fact]
    public void RdpInputMapper_UnmappedKeys_EmpiricalTest()
    {
        // Test unmapped / unusual keys
        bool resFn = RdpInputMapper.TryMapKey((Key)9999, isDown: true, out var kbEventFn);
        Assert.False(resFn);
        Assert.Equal((ushort)0, kbEventFn.KeyCode);

        bool resNone = RdpInputMapper.TryMapKey(Key.None, isDown: true, out var kbEventNone);
        Assert.False(resNone);

        // Test mapped normal key
        bool resA = RdpInputMapper.TryMapKey(Key.A, isDown: true, out var kbEventA);
        Assert.True(resA);
        Assert.Equal((ushort)0x1E, kbEventA.KeyCode);
        Assert.Equal(RdpKeyboardFlags.Down, kbEventA.Flags);

        // Test mapped extended key
        bool resUp = RdpInputMapper.TryMapKey(Key.Up, isDown: false, out var kbEventUp);
        Assert.True(resUp);
        Assert.Equal((ushort)0x48, kbEventUp.KeyCode);
        Assert.Equal(RdpKeyboardFlags.Release | RdpKeyboardFlags.Extended, kbEventUp.Flags);
    }
}
