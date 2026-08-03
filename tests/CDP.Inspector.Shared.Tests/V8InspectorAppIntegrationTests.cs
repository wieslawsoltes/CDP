using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Models;
using CdpInspectorApp.Views;
using CdpInspectorApp.ViewModels;

namespace Avalonia.Diagnostics.Cdp.Tests;

public sealed class V8InspectorAppIntegrationTests
{
    [AvaloniaFact(Timeout = 60_000)]
    public async Task FullInspectorMaintainsPausedNodeSessionAndExpandsVariables()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetAvailablePort();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v8-debug-target.js");
        Assert.True(File.Exists(fixture), $"Missing V8 fixture: {fixture}");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { $"--inspect-brk=127.0.0.1:{port}", fixture },
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });
        Assert.NotNull(process);

        var service = new CdpService();
        var mainViewModel = new MainWindowViewModel(service, loadState: false);
        var mainView = new MainView(mainViewModel, refreshTargetsOnLoad: false);
        var window = new Window { Content = mainView };
        window.Show();
        try
        {
            var endpoint = $"http://127.0.0.1:{port}";
            var target = Assert.Single(await WaitForTargetsAsync(service, endpoint, cancellationToken));
            var sources = mainViewModel.Sources;
            var pauses = Channel.CreateUnbounded<JsonObject>();
            service.EventReceived += (_, e) =>
            {
                if (e.Method == "Debugger.paused") pauses.Writer.TryWrite(e.Params);
            };
            var scriptUrl = new Uri(fixture).AbsoluteUri;
            sources.V8Breakpoints.Add(new V8BreakpointModel
            {
                Key = $"{scriptUrl}:4",
                Url = scriptUrl,
                BindingUrl = scriptUrl,
                LineNumber = 4,
                ColumnNumber = 0,
                IsEnabled = true
            });

            await service.ConnectAsync(endpoint, target, autoResume: false);
            await WaitUntilAsync(() => sources.IsDebuggerEnabled && sources.V8Breakpoints[0].BreakpointId.Length > 0,
                cancellationToken);
            await service.SendCommandAsync("Runtime.runIfWaitingForDebugger");
            while (true)
            {
                var pause = await pauses.Reader.ReadAsync(cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                var frame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(pause["callFrames"])[0]);
                if (frame["functionName"]?.GetValue<string>() == "compute") break;
                await service.SendCommandAsync("Debugger.resume");
            }
            await WaitUntilAsync(() => sources.IsDebuggerPaused && sources.SelectedCallFrame?.FunctionName == "compute",
                cancellationToken);
            // Cross ClientWebSocket's otherwise-default 30-second heartbeat boundary.
            await Task.Delay(TimeSpan.FromSeconds(32), cancellationToken);
            Assert.True(service.IsConnected);

            var localScope = Assert.Single(sources.ScopeVariables, variable => variable.ScopeType == "local");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => localScope.IsExpanded = true);
            await WaitUntilAsync(() => localScope.Children.Any(variable => !variable.IsPlaceholder), cancellationToken);
            var state = Assert.Single(localScope.Children, variable => variable.Name == "state");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => state.IsExpanded = true);
            await WaitUntilAsync(() => state.Children.Any(variable => !variable.IsPlaceholder), cancellationToken);
            Assert.Contains(state.Children, variable => variable.Name == "nested");

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            Assert.True(service.IsConnected);
        }
        finally
        {
            window.Close();
            await service.DisconnectAsync();
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<IReadOnlyList<TargetItem>> WaitForTargetsAsync(
        CdpService service,
        string endpoint,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                var targets = await service.GetTargetsAsync(endpoint);
                if (targets.Count > 0) return targets;
            }
            catch (Exception ex) when (ex.InnerException is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("Node V8 Inspector discovery endpoint did not become ready.", lastError);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(25, cancellationToken);
        }
        Assert.Fail("Condition was not met before the timeout.");
    }
}
