using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class PerformanceDomain
{
    private static readonly ConcurrentDictionary<CdpSession, SessionPerformanceState> _sessionStates = new();
    private static DateTime _lastCpuTime = DateTime.UtcNow;
    private static TimeSpan _lastTotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
    private static readonly int _processorCount = Environment.ProcessorCount;

    public static void CleanupSession(CdpSession session)
    {
        if (_sessionStates.TryRemove(session, out var state))
        {
            state.Dispose();
        }
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    var state = GetOrCreateState(session);
                    state.Enable();
                    return new JsonObject();
                }

            case "disable":
                {
                    if (_sessionStates.TryRemove(session, out var state))
                    {
                        state.Disable();
                    }
                    return new JsonObject();
                }

            case "setTimeDomain":
                return new JsonObject();

            case "getMetrics":
                {
                    var state = GetOrCreateState(session);
                    var metrics = await state.GetMetricsAsync();
                    return new JsonObject
                    {
                        ["metrics"] = metrics
                    };
                }

            default:
                throw new Exception($"Method Performance.{action} is not implemented");
        }
    }

    private static SessionPerformanceState GetOrCreateState(CdpSession session)
    {
        return _sessionStates.GetOrAdd(session, s => new SessionPerformanceState(s));
    }

    private static double GetCpuUsage()
    {
        var now = DateTime.UtcNow;
        var cpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        var elapsed = now - _lastCpuTime;
        if (elapsed.TotalMilliseconds <= 0) return 0.0;

        var usage = (cpuTime - _lastTotalProcessorTime).TotalMilliseconds / (elapsed.TotalMilliseconds * _processorCount) * 100;
        _lastCpuTime = now;
        _lastTotalProcessorTime = cpuTime;
        return Math.Min(100.0, Math.Max(0.0, usage));
    }

    private static int CountVisuals(Visual visual)
    {
        int count = 1;
        foreach (var child in visual.GetVisualChildren())
        {
            count += CountVisuals(child);
        }
        return count;
    }

    private class SessionPerformanceState : IDisposable
    {
        private readonly CdpSession _session;
        private readonly TopLevel? _window;
        private CancellationTokenSource? _loopCts;
        private bool _isHooked;
        private long _lastFrameTimestamp;

        public DispatcherWatchdog Watchdog { get; } = new();
        public int LayoutCount { get; private set; }
        public double Fps { get; private set; }
        public double LastFrameDurationMs { get; private set; }
        public double LayoutDurationMs { get; private set; }

        public SessionPerformanceState(CdpSession session)
        {
            _session = session;
            _window = session.Window;
        }

        public void Enable()
        {
            Hook();
            StartPushLoop();
        }

        public void Disable()
        {
            StopPushLoop();
            Unhook();
        }

        private void Hook()
        {
            if (_isHooked || _window == null) return;

            _window.LayoutUpdated += OnLayoutUpdated;
            try
            {
                var rendererProp = _window.GetType().GetProperty("Renderer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var renderer = rendererProp?.GetValue(_window);
                if (renderer != null)
                {
                    var sceneInvalidatedEvent = renderer.GetType().GetEvent("SceneInvalidated", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (sceneInvalidatedEvent != null)
                    {
                        Console.WriteLine($"[CDP] sceneInvalidatedEvent.EventHandlerType: {sceneInvalidatedEvent.EventHandlerType}");
                        var invokeMethod = sceneInvalidatedEvent.EventHandlerType.GetMethod("Invoke");
                        if (invokeMethod != null)
                        {
                            Console.WriteLine($"[CDP] Invoke parameters: {string.Join(", ", invokeMethod.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"))}");
                        }
                        var handler = Delegate.CreateDelegate(sceneInvalidatedEvent.EventHandlerType!, this, nameof(OnSceneInvalidated));
                        sceneInvalidatedEvent.AddEventHandler(renderer, handler);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CDP] Failed to hook SceneInvalidated: {ex}");
            }
            _isHooked = true;
        }

        private void Unhook()
        {
            if (!_isHooked || _window == null) return;

            _window.LayoutUpdated -= OnLayoutUpdated;
            try
            {
                var rendererProp = _window.GetType().GetProperty("Renderer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var renderer = rendererProp?.GetValue(_window);
                if (renderer != null)
                {
                    var sceneInvalidatedEvent = renderer.GetType().GetEvent("SceneInvalidated", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (sceneInvalidatedEvent != null)
                    {
                        var handler = Delegate.CreateDelegate(sceneInvalidatedEvent.EventHandlerType!, this, nameof(OnSceneInvalidated));
                        sceneInvalidatedEvent.RemoveEventHandler(renderer, handler);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CDP] Failed to unhook SceneInvalidated: {ex.Message}");
            }
            _isHooked = false;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            LayoutCount++;
            var sw = Stopwatch.StartNew();
            Dispatcher.UIThread.Post(() =>
            {
                sw.Stop();
                LayoutDurationMs = sw.Elapsed.TotalMilliseconds;

                if (TracingDomain.IsTracing(_session))
                {
                    var traceEvent = TracingDomain.CreateManualTraceEvent(
                        cat: "Avalonia.Layout",
                        name: "LayoutPass",
                        durationMs: sw.Elapsed.TotalMilliseconds
                    );
                    _ = _session.SendEventAsync("Tracing.dataCollected", new JsonObject
                    {
                        ["value"] = new JsonArray { traceEvent }
                    });
                }
            }, DispatcherPriority.Normal);
        }

        public void OnSceneInvalidated(object? sender, Avalonia.Rendering.SceneInvalidatedEventArgs e)
        {
            long currentTimestamp = Stopwatch.GetTimestamp();
            if (_lastFrameTimestamp != 0)
            {
                double elapsedMs = (currentTimestamp - _lastFrameTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs > 0)
                {
                    Fps = 1000.0 / elapsedMs;
                }
            }
            _lastFrameTimestamp = currentTimestamp;

            var sw = Stopwatch.StartNew();
            Dispatcher.UIThread.Post(() =>
            {
                sw.Stop();
                LastFrameDurationMs = sw.Elapsed.TotalMilliseconds;

                if (TracingDomain.IsTracing(_session))
                {
                    var traceEvent = TracingDomain.CreateManualTraceEvent(
                        cat: "Avalonia.Rendering",
                        name: "RenderFrame",
                        durationMs: sw.Elapsed.TotalMilliseconds
                    );
                    _ = _session.SendEventAsync("Tracing.dataCollected", new JsonObject
                    {
                        ["value"] = new JsonArray { traceEvent }
                    });
                }
            }, DispatcherPriority.Render);
        }

        private void StartPushLoop()
        {
            if (_loopCts != null) return;
            _loopCts = new CancellationTokenSource();
            var token = _loopCts.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(1000, token);
                        if (token.IsCancellationRequested) break;

                        var metrics = await GetMetricsAsync();
                        await _session.SendEventAsync("Performance.metrics", new JsonObject
                        {
                            ["metrics"] = metrics,
                            ["title"] = "metrics"
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }
                }
            });
        }

        private void StopPushLoop()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
        }

        public async Task<JsonArray> GetMetricsAsync()
        {
            double timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            double jsHeapUsedSize = Process.GetCurrentProcess().WorkingSet64;
            double jsHeapTotalSize = GC.GetTotalMemory(false);
            double cpuUsage = GetCpuUsage();

            int nodesCount = 0;
            if (_window != null)
            {
                nodesCount = await Dispatcher.UIThread.InvokeAsync(() => CountVisuals(_window));
            }

            var metricsArray = new JsonArray
            {
                new JsonObject { ["name"] = "Timestamp", ["value"] = timestamp },
                new JsonObject { ["name"] = "Nodes", ["value"] = nodesCount },
                new JsonObject { ["name"] = "JSHeapUsedSize", ["value"] = jsHeapUsedSize },
                new JsonObject { ["name"] = "JSHeapTotalSize", ["value"] = jsHeapTotalSize },
                new JsonObject { ["name"] = "CPUUsage", ["value"] = cpuUsage },
                new JsonObject { ["name"] = "LayoutCount", ["value"] = LayoutCount },
                new JsonObject { ["name"] = "LayoutDuration", ["value"] = LayoutDurationMs / 1000.0 },
                new JsonObject { ["name"] = "FPS", ["value"] = Fps },
                new JsonObject { ["name"] = "FrameDuration", ["value"] = LastFrameDurationMs / 1000.0 },
                new JsonObject { ["name"] = "DispatcherQueueDelay", ["value"] = Watchdog.QueueDelaySeconds },
                new JsonObject { ["name"] = "UIThreadBlockingTime", ["value"] = Watchdog.BlockingTimeSeconds }
            };

            return metricsArray;
        }

        public void Dispose()
        {
            Disable();
            Watchdog.Dispose();
        }
    }

    private class DispatcherWatchdog : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private double _queueDelayMs;
        private double _blockingTimeMs;

        public double QueueDelaySeconds => _queueDelayMs / 1000.0;
        public double BlockingTimeSeconds => _blockingTimeMs / 1000.0;

        public DispatcherWatchdog()
        {
            Task.Run(WatchdogLoopAsync);
        }

        private async Task WatchdogLoopAsync()
        {
            var stopwatch = new Stopwatch();
            while (!_cts.IsCancellationRequested)
            {
                long scheduleTime = Stopwatch.GetTimestamp();
                var uiSignal = new SemaphoreSlim(0, 1);

                Dispatcher.UIThread.Post(() =>
                {
                    long executeTime = Stopwatch.GetTimestamp();
                    _queueDelayMs = (executeTime - scheduleTime) * 1000.0 / Stopwatch.Frequency;
                    try
                    {
                        uiSignal.Release();
                    }
                    catch (ObjectDisposedException) { }
                }, DispatcherPriority.Normal);

                stopwatch.Restart();
                bool completed = false;
                try
                {
                    completed = await uiSignal.WaitAsync(100, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    uiSignal.Dispose();
                    break;
                }
                stopwatch.Stop();

                if (!completed)
                {
                    _blockingTimeMs = stopwatch.ElapsedMilliseconds - 100;
                    try
                    {
                        await uiSignal.WaitAsync(_cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        uiSignal.Dispose();
                        break;
                    }
                }
                else
                {
                    _blockingTimeMs = 0;
                }

                uiSignal.Dispose();

                try
                {
                    await Task.Delay(100, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
