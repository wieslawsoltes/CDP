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

    /// <summary>Time provider whose clock stands still and whose timers fire only when the test releases them.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private readonly List<ManualTimer> _pending = new();
        private readonly object _gate = new();
        private long _nowTicks = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _nowTicks);

        public override DateTimeOffset GetUtcNow() => new(GetTimestamp(), TimeSpan.Zero);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
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

        /// <summary>Advances the clock to the due time of the oldest wait and fires it.</summary>
        public void ElapseNext()
        {
            ManualTimer next;
            lock (_gate)
            {
                next = _pending[0];
                _pending.RemoveAt(0);
            }
            Interlocked.Exchange(ref _nowTicks, Math.Max(GetTimestamp(), next.DueTicks));
            next.Fire();
        }

        private void Schedule(ManualTimer timer, TimeSpan dueTime)
        {
            lock (_gate)
            {
                _pending.Remove(timer);
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    timer.DueTicks = GetTimestamp() + dueTime.Ticks;
                    _pending.Add(timer);
                }
            }
        }

        private void Remove(ManualTimer timer)
        {
            lock (_gate)
            {
                _pending.Remove(timer);
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTime _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;

            public ManualTimer(ManualTime owner, TimerCallback callback, object? state)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
            }

            public long DueTicks { get; set; }

            public void Fire() => _callback(_state);

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                // The scheduler only uses one-shot delays.
                _owner.Schedule(this, dueTime);
                return true;
            }

            public void Dispose() => _owner.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
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
        var scheduler = new CoalescingReloadScheduler(() => { Interlocked.Increment(ref reloads); return Task.CompletedTask; }, Quiet, MaxDelay, time);

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

        var scheduler = new CoalescingReloadScheduler(Reload, Quiet, MaxDelay, time);

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
        var scheduler = new CoalescingReloadScheduler(() => { Interlocked.Increment(ref reloads); return Task.CompletedTask; }, Quiet, MaxDelay, time);

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

    [Fact]
    public async Task FailingReload_IsCounted_AndLaterRequestStillReloads()
    {
        var time = new ManualTime();
        int reloads = 0;
        var scheduler = new CoalescingReloadScheduler(() =>
        {
            if (Interlocked.Increment(ref reloads) == 1)
            {
                throw new InvalidOperationException("reload failed");
            }
            return Task.CompletedTask;
        }, Quiet, MaxDelay, time);

        scheduler.Request();
        Assert.True(await time.WaitForPendingDelayAsync());
        time.ElapseNext();
        await scheduler.WhenIdleAsync();

        Assert.Equal(1, reloads);
        Assert.Equal(1, scheduler.FailedReloadCount);
        Assert.False(scheduler.IsBusy);

        // The failure must neither stop the scheduler nor leave it busy.
        scheduler.Request();
        Assert.True(await time.WaitForPendingDelayAsync());
        time.ElapseNext();
        await scheduler.WhenIdleAsync();

        Assert.Equal(2, reloads);
        Assert.Equal(2, scheduler.ReloadCount);
        Assert.Equal(1, scheduler.FailedReloadCount);
        Assert.False(scheduler.IsBusy);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
