using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ReconciliationActivityMonitorTests
{
    [Fact]
    public async Task ActiveJellyfinScanDefersUntilTheScanStops()
    {
        var isScanRunning = true;
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.SetupGet(value => value.IsScanRunning).Returns(() => isScanRunning);
        var delay = new CallbackDelay(() => isScanRunning = false);
        var monitor = new ReconciliationActivityMonitor(
            libraryManager.Object,
            TimeProvider.System,
            delay);

        await monitor.WaitUntilQuietAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal([TimeSpan.FromSeconds(1)], delay.Delays);
    }

    [Fact]
    public async Task RecentEligibleEventDefersUntilTheTenSecondQuietWindowExpires()
    {
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.SetupGet(value => value.IsScanRunning).Returns(false);
        var timeProvider = new ManualTimeProvider();
        var delay = new AdvancingDelay(timeProvider);
        var monitor = new ReconciliationActivityMonitor(
            libraryManager.Object,
            timeProvider,
            delay);
        monitor.RecordActivity();

        await monitor.WaitUntilQuietAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal([TimeSpan.FromSeconds(10)], delay.Delays);
    }

    private sealed class CallbackDelay : IReconciliationDelay
    {
        private readonly Action _callback;

        public CallbackDelay(Action callback)
        {
            _callback = callback;
        }

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            _callback();
            return Task.CompletedTask;
        }
    }

    private sealed class AdvancingDelay : IReconciliationDelay
    {
        private readonly ManualTimeProvider _timeProvider;

        public AdvancingDelay(ManualTimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            _timeProvider.Advance(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
