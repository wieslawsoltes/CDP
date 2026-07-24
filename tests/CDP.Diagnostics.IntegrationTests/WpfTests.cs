using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wpf.Diagnostics.Cdp;
using Xunit;

namespace CDP.Diagnostics.IntegrationTests;

public class WpfTests
{
    private Exception? _threadException;

    private void RunInSta(Action action)
    {
        _threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (_threadException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_threadException).Throw();
        }
    }

    [Fact]
    public void TestWpfPopupAndSecondaryWindowSupport()
    {
        if (!OperatingSystem.IsWindows()) return;

        RunInSta(() =>
        {
            CdpServer.EnsureInitialized();

            var mainGrid = new Grid { Name = "MainGrid" };
            var mainWindow = new Window
            {
                Title = "MainWindow",
                Content = mainGrid,
                Visibility = Visibility.Visible
            };

            var secondaryGrid = new Grid { Name = "SecondaryGrid" };
            var secondaryWindow = new Window
            {
                Title = "SecondaryWindow",
                Content = secondaryGrid,
                Visibility = Visibility.Visible
            };

            CdpServer.Register(mainWindow, "MainWindow");
            CdpServer.Register(secondaryWindow, "SecondaryWindow");

            try
            {
                // Create Popup and add to mainGrid's children
                var popup = new Popup
                {
                    IsOpen = true
                };
                var popupBtn = new Button { Name = "PopupBtn", Content = "Popup Button" };
                popup.Child = popupBtn;

                mainGrid.Children.Add(popup);

                // 1. Verify GetChildren anchors secondary window and popups to main window
                var children = CdpVisualTreeHelper.GetChildren(mainWindow, false).ToList();
                Assert.Contains(secondaryWindow, children);
                Assert.Contains(popupBtn, children);

                // 2. Verify GetParent maps popup child and secondary window back to main window
                var parentOfSecondary = CdpVisualTreeHelper.GetParent(secondaryWindow, false);
                Assert.Equal(mainWindow, parentOfSecondary);

                var parentOfPopupChild = CdpVisualTreeHelper.GetParent(popupBtn, false);
                Assert.Equal(mainWindow, parentOfPopupChild);

                // 3. Verify Selector locates element inside popup
                var foundBtn = SelectorEngine.QuerySelector(mainWindow, "#PopupBtn");
                Assert.NotNull(foundBtn);
                Assert.Equal(popupBtn, foundBtn);
            }
            finally
            {
                CdpServer.Unregister(mainWindow);
                CdpServer.Unregister(secondaryWindow);
                mainWindow.Close();
                secondaryWindow.Close();
            }
        });
    }

    [Fact]
    public void TestWpfDomHitTesting()
    {
        if (!OperatingSystem.IsWindows()) return;

        RunInSta(() =>
        {
            CdpServer.EnsureInitialized();

            var mainGrid = new Grid { Name = "MainGrid" };
            var mainWindow = new Window
            {
                Title = "MainWindow",
                Content = mainGrid,
                Width = 300,
                Height = 200,
                Visibility = Visibility.Visible
            };

            var mainBtn = new Button { Name = "MainBtn", Content = "Main Button", Width = 100, Height = 50 };
            mainGrid.Children.Add(mainBtn);

            CdpServer.Register(mainWindow, "MainWindow");

            try
            {
                // Force layout/arrange
                mainWindow.Measure(new System.Windows.Size(300, 200));
                mainWindow.Arrange(new Rect(0, 0, 300, 200));

                using var fakeWs = new FakeWebSocket();
                var session = new CdpSession(fakeWs, mainWindow);

                // Call DOM.getNodeForLocation
                var hitParams = new System.Text.Json.Nodes.JsonObject
                {
                    ["x"] = 50,
                    ["y"] = 25
                };

                var hitRes = Wpf.Diagnostics.Cdp.Domains.DomDomain.HandleAsync(session, "getNodeForLocation", hitParams).GetAwaiter().GetResult();
                Assert.NotNull(hitRes);

                int hitId = hitRes["nodeId"]?.GetValue<int>() ?? 0;
                Assert.True(hitId > 0);

                var hitVisual = session.NodeMap.GetVisual(hitId);
                Assert.NotNull(hitVisual);
            }
            finally
            {
                CdpServer.Unregister(mainWindow);
                mainWindow.Close();
            }
        });
    }
}

public class FakeWebSocket : System.Net.WebSockets.WebSocket
{
    public System.Collections.Generic.List<string> SentMessages { get; } = new();
    private System.Net.WebSockets.WebSocketState _state = System.Net.WebSockets.WebSocketState.Open;

    public override System.Net.WebSockets.WebSocketState State => _state;
    public override string? SubProtocol => null;
    public override System.Net.WebSockets.WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;

    public override System.Threading.Tasks.Task SendAsync(System.ArraySegment<byte> buffer, System.Net.WebSockets.WebSocketMessageType messageType, bool endOfMessage, System.Threading.CancellationToken cancellationToken)
    {
        var msg = System.Text.Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        SentMessages.Add(msg);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override System.Threading.Tasks.Task<System.Net.WebSockets.WebSocketReceiveResult> ReceiveAsync(System.ArraySegment<byte> buffer, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.FromResult(new System.Net.WebSockets.WebSocketReceiveResult(0, System.Net.WebSockets.WebSocketMessageType.Close, true));
    }

    public override System.Threading.Tasks.Task CloseAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, System.Threading.CancellationToken cancellationToken)
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override System.Threading.Tasks.Task CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, System.Threading.CancellationToken cancellationToken)
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
    }

    public override void Dispose()
    {
    }
}
