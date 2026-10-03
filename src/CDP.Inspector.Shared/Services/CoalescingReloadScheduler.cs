using System;
using System.Threading;
using System.Threading.Tasks;

namespace CdpInspectorApp.Services;

/// <summary>
/// Merges bursts of reload requests into single reload runs.
/// A request starts a quiet period; every further request inside it restarts the period,
/// capped by a maximum delay so long bursts still refresh periodically. Reloads never overlap:
/// requests that arrive while a reload runs are served by exactly one trailing reload.
/// </summary>
public sealed class CoalescingReloadScheduler
{
    public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMilliseconds(1000);

    private readonly Func<Task> _reload;
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maxDelay;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();
    private long _requestVersion;
    private Task? _loop;
    private int _reloadCount;
    private int _requestCount;

    public CoalescingReloadScheduler(Func<Task> reload)
        : this(reload, DefaultQuietPeriod, DefaultMaxDelay, null, null)
    {
    }

    public CoalescingReloadScheduler(Func<Task> reload, TimeSpan quietPeriod, TimeSpan maxDelay, Func<TimeSpan, Task>? delay, Func<DateTime>? utcNow)
    {
        _reload = reload ?? throw new ArgumentNullException(nameof(reload));
        _quietPeriod = quietPeriod;
        _maxDelay = maxDelay;
        _delay = delay ?? Task.Delay;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Number of reload runs that were started.</summary>
    public int ReloadCount => Volatile.Read(ref _reloadCount);

    /// <summary>Number of reload requests received.</summary>
    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>True while a reload is pending or running.</summary>
    public bool IsBusy
    {
        get
        {
            lock (_gate)
            {
                return _loop != null;
            }
        }
    }

    public void Request()
    {
        Interlocked.Increment(ref _requestCount);
        lock (_gate)
        {
            _requestVersion++;
            if (_loop != null)
            {
                return;
            }
            _loop = RunAsync();
        }
    }

    /// <summary>Completes when no reload is pending or running.</summary>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task? loop;
            lock (_gate)
            {
                loop = _loop;
            }
            if (loop == null)
            {
                return;
            }
            await loop.ConfigureAwait(false);
        }
    }

    private async Task RunAsync()
    {
        // Leave the caller's stack first so a burst of synchronous requests is merged.
        await Task.Yield();

        while (true)
        {
            var burstStart = _utcNow();
            long handledVersion;
            while (true)
            {
                long seenVersion;
                lock (_gate)
                {
                    seenVersion = _requestVersion;
                }

                var remaining = _maxDelay - (_utcNow() - burstStart);
                if (remaining <= TimeSpan.Zero)
                {
                    handledVersion = seenVersion;
                    break;
                }
                await _delay(remaining < _quietPeriod ? remaining : _quietPeriod).ConfigureAwait(false);

                lock (_gate)
                {
                    if (_requestVersion == seenVersion)
                    {
                        handledVersion = seenVersion;
                        break;
                    }
                }
            }

            Interlocked.Increment(ref _reloadCount);
            try
            {
                await _reload().ConfigureAwait(false);
            }
            catch
            {
                // The reload delegate reports its own errors; a failed run must not stop later reloads.
            }

            lock (_gate)
            {
                if (_requestVersion == handledVersion)
                {
                    _loop = null;
                    return;
                }
            }
        }
    }
}
