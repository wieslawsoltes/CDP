using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CdpInspectorApp.Services;
using Xunit;

namespace CDP.Inspector.Shared.Tests;

public class CoalescingReloadSchedulerTests
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(1000);

    /// <summary>Delay source whose waits complete only when the test releases them.</summary>
    private sealed class ManualTime
    {
        private readonly List<(TimeSpan Duration, TaskCompletionSource Completion)> _pending = new();
        private readonly object _gate = new();
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Task Delay(TimeSpan duration)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _pending.Add((duration, completion));
            }
            return completion.Task;
        }

        /// <summary>Returns true once a wait is pending, false once <paramref name="stop"/> holds first.</summary>
        public async Task<bool> WaitForPendingDelayAsync(Func<bool>? stop = null)
        {
            for (int i = 0; i < 1000; i++)
            {
                lock (_gate)
                {
                    if (_pending.Count > 0) return true;
                }
                if (stop != null && stop()) return false;
                await Task.Delay(5);
            }
            throw new TimeoutException("No pending delay");
        }

        /// <summary>Advances the clock by the requested duration of the oldest wait and completes it.</summary>
        public void ElapseNext()
        {
            (TimeSpan Duration, TaskCompletionSource Completion) next;
            lock (_gate)
            {
                next = _pending[0];
                _pending.RemoveAt(0);
            }
            UtcNow += next.Duration;
            next.Completion.SetResult();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(5);
        }
        Assert.True(condition());
    }

    [Fact]
    public async Task Burst_IsMergedIntoOneReload()
    {
        var time = new ManualTime();
        int reloads = 0;
        var scheduler = new CoalescingReloadScheduler(() => { Interlocked.Increment(ref reloads); return Task.CompletedTask; }, Quiet, MaxDelay, time.Delay, () => time.UtcNow);

        for (int i = 0; i < 2000; i++)
        {
            scheduler.Request();
        }

        // The quiet period may restart while the burst is still being observed; release waits until the reload ran.
        while (await time.WaitForPendingDelayAsync(() => Volatile.Read(ref reloads) > 0))
        {
            time.ElapseNext();
        }
        await scheduler.WhenIdleAsync();

        Assert.Equal(1, reloads);
        Assert.Equal(1, scheduler.ReloadCount);
        Assert.Equal(2000, scheduler.RequestCount);
        Assert.False(scheduler.IsBusy);
    }

    [Fact]
    public async Task RequestsDuringReload_RunExactlyOneTrailingReload_WithoutOverlap()
    {
        var time = new ManualTime();
        int running = 0;
        int maxRunning = 0;
        int reloads = 0;
        var releaseReload = new List<TaskCompletionSource>();
        var gate = new object();

        async Task Reload()
        {
            var current = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, current);
            Interlocked.Increment(ref reloads);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                releaseReload.Add(release);
            }
            await release.Task;
            Interlocked.Decrement(ref running);
        }

        var scheduler = new CoalescingReloadScheduler(Reload, Quiet, MaxDelay, time.Delay, () => time.UtcNow);

        scheduler.Request();
        Assert.True(await time.WaitForPendingDelayAsync());
        time.ElapseNext();
        await WaitUntilAsync(() => Volatile.Read(ref reloads) == 1);

        // The first reload is still running: these requests must neither start a parallel reload nor be lost.
        for (int i = 0; i < 500; i++)
        {
            scheduler.Request();
        }
        await Task.Delay(20);
        Assert.Equal(1, Volatile.Read(ref reloads));

        lock (gate)
        {
            releaseReload[0].SetResult();
        }
        Assert.True(await time.WaitForPendingDelayAsync());
        time.ElapseNext();
        await WaitUntilAsync(() => Volatile.Read(ref reloads) == 2);
        lock (gate)
        {
            releaseReload[1].SetResult();
        }
        await scheduler.WhenIdleAsync();

        Assert.Equal(2, reloads);
        Assert.Equal(1, maxRunning);
        Assert.False(scheduler.IsBusy);
    }

    [Fact]
    public async Task ContinuousRequests_StillReloadAfterMaxDelay()
    {
        var time = new ManualTime();
        int reloads = 0;
        var scheduler = new CoalescingReloadScheduler(() => { Interlocked.Increment(ref reloads); return Task.CompletedTask; }, Quiet, MaxDelay, time.Delay, () => time.UtcNow);

        scheduler.Request();
        int waits = 0;
        while (await time.WaitForPendingDelayAsync(() => Volatile.Read(ref reloads) > 0))
        {
            // A new request inside every quiet period would postpone the reload forever without the cap.
            scheduler.Request();
            time.ElapseNext();
            waits++;
            Assert.True(waits <= 20, "Reload was postponed beyond the maximum delay");
        }

        // Ten quiet periods of 100 ms reach the 1000 ms cap; the reload then covers every request so far.
        Assert.Equal(10, waits);
        await scheduler.WhenIdleAsync();
        Assert.Equal(1, reloads);
        Assert.Equal(11, scheduler.RequestCount);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
