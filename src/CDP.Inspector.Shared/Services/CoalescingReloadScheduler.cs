using System;
using System.Threading;
using System.Threading.Tasks;
using Chrome.DevTools.Protocol;
using Microsoft.Extensions.Logging;

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

    private static readonly ILogger Logger = CdpLogging.CreateLogger<CoalescingReloadScheduler>();

    private readonly Func<Task> _reload;
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maxDelay;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private long _requestVersion;
    private Task? _loop;
    private int _reloadCount;
    private int _requestCount;
    private int _failedReloadCount;

    public CoalescingReloadScheduler(Func<Task> reload)
        : this(reload, DefaultQuietPeriod, DefaultMaxDelay, null)
    {
    }

    /// <param name="timeProvider">Clock and delay source; <see cref="TimeProvider.System"/> when null.</param>
    public CoalescingReloadScheduler(Func<Task> reload, TimeSpan quietPeriod, TimeSpan maxDelay, TimeProvider? timeProvider)
    {
        _reload = reload ?? throw new ArgumentNullException(nameof(reload));
        _quietPeriod = quietPeriod;
        _maxDelay = maxDelay;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Number of reload runs that were started.</summary>
    public int ReloadCount => Volatile.Read(ref _reloadCount);

    /// <summary>Number of reload runs that failed with an exception.</summary>
    public int FailedReloadCount => Volatile.Read(ref _failedReloadCount);

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
            var burstStart = _timeProvider.GetTimestamp();
            long handledVersion;
            while (true)
            {
                long seenVersion;
                lock (_gate)
                {
                    seenVersion = _requestVersion;
                }

                var remaining = _maxDelay - _timeProvider.GetElapsedTime(burstStart);
                if (remaining <= TimeSpan.Zero)
                {
                    handledVersion = seenVersion;
                    break;
                }
                await Task.Delay(remaining < _quietPeriod ? remaining : _quietPeriod, _timeProvider).ConfigureAwait(false);

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
            catch (Exception ex)
            {
                // A failed run must not stop later reloads.
                Interlocked.Increment(ref _failedReloadCount);
                Logger.LogWarningMessage(nameof(CoalescingReloadScheduler), "Reload failed", ex);
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
