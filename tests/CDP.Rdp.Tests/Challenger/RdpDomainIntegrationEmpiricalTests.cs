namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
using SkiaSharp;
using Xunit;

public class RdpDomainIntegrationEmpiricalTests
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

    [AvaloniaFact]
    public async Task DOM_Domain_VisualTreeTraversal_And_BoxModel_Stress()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Get Document
        var docResult = await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["depth"] = -1, ["pierce"] = true });
        Assert.NotNull(docResult);
        Assert.NotNull(docResult["root"]);
        int rootNodeId = docResult["root"]!["nodeId"]!.GetValue<int>();

        // 2. High frequency querySelector calls across various controls
        string[] selectors = new[]
        {
            "#txtHost",
            "#txtPort",
            "#txtUsername",
            "#txtPassword",
            "#btnConnect",
            "#btnDisconnect",
            "#rdpPreviewControl",
            "#TabPreview",
            "#TabElements",
            "#TabRecorder",
            "#imgScreenshot",
            "#lstVisualTree"
        };

        for (int iteration = 0; iteration < 10; iteration++)
        {
            foreach (var sel in selectors)
            {
                var queryResult = await DomDomain.HandleAsync(session, "querySelector", new JsonObject
                {
                    ["nodeId"] = rootNodeId,
                    ["selector"] = sel
                });
                Assert.NotNull(queryResult);
                int nodeId = queryResult["nodeId"]!.GetValue<int>();
                Assert.True(nodeId > 0, $"Selector {sel} failed to resolve a valid nodeId");

                var boxResult = await DomDomain.HandleAsync(session, "getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                Assert.NotNull(boxResult);
                var model = boxResult["model"];
                Assert.NotNull(model);
                Assert.NotNull(model["content"]);
                Assert.Equal(8, model["content"]!.AsArray().Count);
            }
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task Input_Domain_DispatchMouseEvent_HighFrequency_Boundary_Stress()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Send 500 rapid mouse movement & click dispatches with boundary/edge coordinates
        var random = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            double x = (i % 5 == 0) ? -50.5 : (i % 5 == 1) ? 2000.75 : random.NextDouble() * 800;
            double y = (i % 5 == 0) ? -10.0 : (i % 5 == 1) ? 1500.25 : random.NextDouble() * 600;
            string type = (i % 3 == 0) ? "mouseMoved" : (i % 3 == 1) ? "mousePressed" : "mouseReleased";
            string button = (i % 2 == 0) ? "left" : "right";

            var res = await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
            {
                ["type"] = type,
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = 1
            });
            Assert.NotNull(res);
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task Input_Domain_InsertText_Unicode_Emoji_SurrogatePairs_Stress()
    {
        var window = new MainWindow();
        window.Show();

        var txtHost = window.FindControl<TextBox>("txtHost");
        Assert.NotNull(txtHost);
        txtHost.AcceptsReturn = true;
        txtHost.AcceptsTab = true;
        txtHost.Focus();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Unicode strings including CJK, Cyrillic, Arabic, Accented Latin, Emojis, ZWJ sequences
        string[] unicodePayloads = new[]
        {
            "Standard_192.168.1.1",
            "こんにちは世界_10.0.0.1",
            "Привет_Мир_172.16.0.1",
            "مرحبا_بك_CDP",
            "Éléphant_à_la_crème",
            "🚀🔑🎯⚡💎🐉🌍🔥🎉",
            "👨‍👩‍👧‍👦_ZWJ_Test",
            "Line1\nLine2\tTabbed"
        };

        foreach (var payload in unicodePayloads)
        {
            txtHost.Text = string.Empty;

            await InputDomain.HandleAsync(session, "insertText", new JsonObject
            {
                ["text"] = payload
            });

            Assert.Equal(payload, txtHost.Text);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void RdpControl_OnTextInput_SurrogatePair_Handling_Empirical_Check()
    {
        var rdpControl = new RdpControl();
        var dummySession = new DummyRdpSession();
        rdpControl.Session = dummySession;

        // Test multi-byte emoji character consisting of UTF-16 surrogate pair (High surrogate 0xD83D, Low surrogate 0xDE80 = 🚀)
        string emojiStr = "🚀";
        Assert.Equal(2, emojiStr.Length); // 2 UTF-16 code units

        var args = new TextInputEventArgs
        {
            Text = emojiStr,
            RoutedEvent = InputElement.TextInputEvent
        };

        rdpControl.RaiseEvent(args);

        // EnumerateRunes produces 1 Rune scalar (1 Down + 1 Release = 2 events total) preventing surrogate pair splitting
        Assert.Equal(2, dummySession.SentInputEvents.Count);

        // Verify all emitted events are Unicode type
        foreach (var evt in dummySession.SentInputEvents)
        {
            Assert.Equal(RdpInputMessageType.Unicode, evt.MessageType);
        }

        uint scalarVal = (uint)new System.Text.Rune(0x1F680).Value;
        Assert.Equal(scalarVal, dummySession.SentInputEvents[0].KeyboardEvent.KeyCode);
        Assert.Equal(scalarVal, dummySession.SentInputEvents[1].KeyboardEvent.KeyCode);
    }

    [AvaloniaFact]
    public async Task Page_Domain_Screencast_HighThroughput_DirtyRegion_Stress()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Enable Page and start screencast
        await PageDomain.HandleAsync(session, "enable", new JsonObject());
        await PageDomain.HandleAsync(session, "startScreencast", new JsonObject
        {
            ["format"] = "png",
            ["everyNthFrame"] = 1
        });

        // 2. Capture screenshot directly
        var screenshotResult = await PageDomain.HandleAsync(session, "captureScreenshot", new JsonObject { ["format"] = "png" });
        Assert.NotNull(screenshotResult);
        string? data = screenshotResult["data"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(data));

        // 3. Stress dirty region rendering with 100 frame updates
        using var buffer = new RdpFrameBuffer(1280, 720);
        byte[] tileData = new byte[64 * 64 * 4];
        Array.Fill(tileData, (byte)0xAA);

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            ushort x = (ushort)((i * 12) % 1200);
            ushort y = (ushort)((i * 8) % 650);
            var update = new RdpBitmapUpdate(x, y, 64, 64, 32, compressed: false, tileData);
            buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs((ulong)i, DateTimeOffset.UtcNow, new[] { update }));
            buffer.SwapBuffers();

            session.RequestScreencastFrame();
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, $"100 frame updates took {sw.ElapsedMilliseconds} ms, expected < 5000 ms");

        await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Runtime_Domain_Evaluate_ComplexExpressions_ExceptionHandling_Stress()
    {
        var window = new MainWindow();
        window.Show();

        var vm = (MainWindowViewModel)window.DataContext!;
        vm.Host = "192.168.1.100";
        vm.Port = 3389;

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        await RuntimeDomain.HandleAsync(session, "enable", new JsonObject());

        // 1. Valid C# expressions
        var evalHost = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "DataContext.Host"
        });
        Assert.Equal("192.168.1.100", evalHost["result"]?["value"]?.GetValue<string>());

        var evalPort = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "DataContext.Port"
        });
        Assert.Equal(3389, (int)evalPort["result"]!["value"]!.GetValue<double>());

        // 2. JS DOM facade expressions
        var evalDocId = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "document.querySelector('#btnConnect').id"
        });
        Assert.Equal("btnConnect", evalDocId["result"]?["value"]?.GetValue<string>());

        // 3. Exception propagation and error expressions
        var evalError = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "NonExistentProperty.Property"
        });
        Assert.NotNull(evalError);
        Assert.True(evalError.ContainsKey("exceptionDetails") || evalError.ContainsKey("result"));

        // 4. High frequency evaluation loop (200 iterations)
        for (int i = 0; i < 200; i++)
        {
            var res = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
            {
                ["expression"] = "Window.Title"
            });
            Assert.NotNull(res);
        }

        window.Close();
    }
}
