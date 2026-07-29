namespace CDP.Rdp.Tests.Domains;

using System;
using System.Collections.Generic;
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
using Xunit;

[Xunit.Collection("RdpTests")]
public class CdpRdpInputDomainTests
{
    private class DummyRdpSession : IRdpSession
    {
        public RdpConnectionState State { get; set; } = RdpConnectionState.Disconnected;
        public RdpSessionOptions Options { get; } = new RdpSessionOptions();

        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

        public List<RdpInputEvent> SentInputEvents { get; } = new();
        public List<RdpFastPathInputEvent> SentFastPathEvents { get; } = new();

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(RdpConnectionState.Connected);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(RdpConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        private void SetState(RdpConnectionState state)
        {
            RdpConnectionState oldState = State;
            State = state;
            StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(oldState, state));
        }

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
    public async Task DispatchMouseEvent_ClickConnectButton_UpdatesConnectionState()
    {
        var window = new MainWindow();
        var dummy = new DummyRdpSession();
        window.DataContext = new MainWindowViewModel(_ => dummy);
        window.Show();

        try
        {
            var vm = (MainWindowViewModel)window.DataContext!;
            Assert.False(vm.Connection.IsConnected);

            using var clientWs = new ClientWebSocket();
            using var session = new CdpSession(clientWs, window);

            var docResult = await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["depth"] = -1 });
            int rootNodeId = docResult["root"]!["nodeId"]!.GetValue<int>();

            var queryResult = await DomDomain.HandleAsync(session, "querySelector", new JsonObject
            {
                ["nodeId"] = rootNodeId,
                ["selector"] = "#btnConnect"
            });
            int btnNodeId = queryResult["nodeId"]!.GetValue<int>();

            var boxResult = await DomDomain.HandleAsync(session, "getBoxModel", new JsonObject { ["nodeId"] = btnNodeId });
            var quad = boxResult["model"]!["content"]!.AsArray();
            double x = (quad[0]!.GetValue<double>() + quad[2]!.GetValue<double>()) / 2.0;
            double y = (quad[1]!.GetValue<double>() + quad[5]!.GetValue<double>()) / 2.0;

            await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "none"
            });

            await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "left",
                ["clickCount"] = 1
            });

            await InputDomain.HandleAsync(session, "dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "left",
                ["clickCount"] = 1
            });

            DateTime timeout = DateTime.UtcNow.AddSeconds(5);
            while (!vm.Connection.IsConnected && DateTime.UtcNow < timeout)
            {
                await Task.Delay(10);
            }

            Assert.True(vm.Connection.IsConnected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DispatchTextInput_UpdatesFocusedControl_And_TriggersOnTextInput()
    {
        var window = new MainWindow();
        window.Show();

        var txtHost = window.FindControl<TextBox>("txtHost");
        Assert.NotNull(txtHost);
        txtHost.Text = string.Empty;
        txtHost.Focus();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        await InputDomain.HandleAsync(session, "insertText", new JsonObject
        {
            ["text"] = "192.168.1.50"
        });

        Assert.Equal("192.168.1.50", txtHost.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void RdpControl_OnTextInput_SendsUnicodeRdpInputEvents()
    {
        var rdpControl = new RdpControl();
        var dummySession = new DummyRdpSession();
        rdpControl.Session = dummySession;

        var args = new TextInputEventArgs
        {
            Text = "ABC",
            RoutedEvent = InputElement.TextInputEvent
        };

        rdpControl.RaiseEvent(args);

        Assert.Equal(6, dummySession.SentInputEvents.Count);
        Assert.Equal(RdpInputMessageType.Unicode, dummySession.SentInputEvents[0].MessageType);
        Assert.Equal('A', dummySession.SentInputEvents[0].KeyboardEvent.KeyCode);
        Assert.Equal(RdpKeyboardFlags.Down, dummySession.SentInputEvents[0].KeyboardEvent.Flags);

        Assert.Equal(RdpInputMessageType.Unicode, dummySession.SentInputEvents[1].MessageType);
        Assert.Equal('A', dummySession.SentInputEvents[1].KeyboardEvent.KeyCode);
        Assert.Equal(RdpKeyboardFlags.Release, dummySession.SentInputEvents[1].KeyboardEvent.Flags);
    }
}
