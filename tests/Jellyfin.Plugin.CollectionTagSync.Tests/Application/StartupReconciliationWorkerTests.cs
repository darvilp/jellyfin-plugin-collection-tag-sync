using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class StartupReconciliationWorkerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public async Task EnabledMappingQueuesOneStartupRequestAfterConfiguredDelay(int delayMinutes)
    {
        var host = new Mock<IServerApplicationHost>(MockBehavior.Strict);
        host.SetupGet(value => value.CoreStartupHasCompleted).Returns(true);
        var requests = new FullReconcileRequestStore();
        var delay = new RecordingDelay();
        using var worker = new StartupReconciliationWorker(
            host.Object,
            new FixedPersistence(delayMinutes),
            new FixedMappingProvider(enabled: true),
            requests,
            delay,
            NullLogger<StartupReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() => requests.Status.IsRequested).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal([TimeSpan.FromMinutes(delayMinutes)], delay.Delays);
        Assert.Equal([FullReconcileRequestReason.Startup], requests.Status.Reasons);
    }

    [Fact]
    public async Task ZeroDelayStillWaitsUntilJellyfinCoreStartupCompletes()
    {
        var ready = false;
        var host = new Mock<IServerApplicationHost>(MockBehavior.Strict);
        host.SetupGet(value => value.CoreStartupHasCompleted).Returns(() => ready);
        var requests = new FullReconcileRequestStore();
        var delay = new BlockingFirstDelay();
        using var worker = new StartupReconciliationWorker(
            host.Object,
            new FixedPersistence(delayMinutes: 0),
            new FixedMappingProvider(enabled: true),
            requests,
            delay,
            NullLogger<StartupReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await delay.FirstDelayStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        Assert.False(requests.Status.IsRequested);

        ready = true;
        delay.ReleaseFirstDelay();
        await WaitUntilAsync(() => requests.Status.IsRequested).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(StartupReconciliationWorker.ServerReadyPollInterval, delay.Delays[0]);
        Assert.Equal(TimeSpan.Zero, delay.Delays[1]);
    }

    [Fact]
    public async Task DisabledMappingsDoNotQueueStartupRecovery()
    {
        var host = new Mock<IServerApplicationHost>(MockBehavior.Strict);
        host.SetupGet(value => value.CoreStartupHasCompleted).Returns(true);
        var requests = new FullReconcileRequestStore();
        var delay = new RecordingDelay();
        using var worker = new StartupReconciliationWorker(
            host.Object,
            new FixedPersistence(delayMinutes: 5),
            new FixedMappingProvider(enabled: false),
            requests,
            delay,
            NullLogger<StartupReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await Assert.IsAssignableFrom<Task>(worker.ExecuteTask)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(delay.Delays);
        Assert.False(requests.Status.IsRequested);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(true);
        }
    }

    private sealed class FixedPersistence : IPluginConfigurationPersistence
    {
        public FixedPersistence(int delayMinutes)
        {
            Current = new PluginConfiguration { StartupReconcileDelayMinutes = delayMinutes };
        }

        public PluginConfiguration Current { get; }

        public void Save(PluginConfiguration configuration)
        {
            throw new InvalidOperationException("Startup policy must not save configuration.");
        }
    }

    private sealed class FixedMappingProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration? _configuration;

        public FixedMappingProvider(bool enabled)
        {
            _configuration = enabled
                ? Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
                    [
                        new MappingGroupDefinition(
                            new TagNodeDefinition("Target"),
                            [new TagNodeDefinition("Source")],
                            MappingPolicy.Additive,
                            isEnabled: true),
                    ]).Configuration)
                : null;
        }

        public MappingConfiguration? GetConfiguration()
        {
            return _configuration;
        }
    }

    private class RecordingDelay : IReconciliationDelay
    {
        public System.Collections.Generic.List<TimeSpan> Delays { get; } = [];

        public virtual Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingFirstDelay : RecordingDelay
    {
        private readonly TaskCompletionSource _firstDelayStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstDelay = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public Task FirstDelayStarted => _firstDelayStarted.Task;

        public override Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            base.DelayAsync(delay, cancellationToken);
            if (Interlocked.Increment(ref _callCount) != 1)
            {
                return Task.CompletedTask;
            }

            _firstDelayStarted.TrySetResult();
            return _releaseFirstDelay.Task.WaitAsync(cancellationToken);
        }

        public void ReleaseFirstDelay()
        {
            _releaseFirstDelay.TrySetResult();
        }
    }
}
