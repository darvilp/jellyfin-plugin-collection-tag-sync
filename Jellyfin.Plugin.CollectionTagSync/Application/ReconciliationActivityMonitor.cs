using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Uses Jellyfin scan state and a short event settling window to identify a quiet server.
/// </summary>
internal sealed class ReconciliationActivityMonitor : IReconciliationActivityMonitor
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScanPollInterval = TimeSpan.FromSeconds(1);
    private readonly object _sync = new();
    private readonly ILibraryManager _libraryManager;
    private readonly TimeProvider _timeProvider;
    private readonly IReconciliationDelay _delay;
    private DateTimeOffset? _lastActivity;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciliationActivityMonitor"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="timeProvider">The process time provider.</param>
    /// <param name="delay">The cancellable delay boundary.</param>
    public ReconciliationActivityMonitor(
        ILibraryManager libraryManager,
        TimeProvider timeProvider,
        IReconciliationDelay delay)
    {
        _libraryManager = libraryManager;
        _timeProvider = timeProvider;
        _delay = delay;
    }

    /// <inheritdoc />
    public void RecordActivity()
    {
        lock (_sync)
        {
            _lastActivity = _timeProvider.GetUtcNow();
        }
    }

    /// <inheritdoc />
    public async Task WaitUntilQuietAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var delay = GetRequiredDelay();
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await _delay.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetRequiredDelay()
    {
        if (_libraryManager.IsScanRunning)
        {
            return ScanPollInterval;
        }

        lock (_sync)
        {
            if (_lastActivity is null)
            {
                return TimeSpan.Zero;
            }

            var remaining = QuietPeriod - (_timeProvider.GetUtcNow() - _lastActivity.Value);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
