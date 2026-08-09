using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ReconciliationSerializationTests
{
    [Fact]
    public async Task CoordinatorNeverReadsSecondItemWhileFirstItemIsRunning()
    {
        var firstId = new Guid("18cef853-6c09-4adc-a6ec-c5b7c4dc26bd");
        var secondId = new Guid("2365afff-9bc2-4327-855f-70b432864cce");
        var reader = new BlockingStateReader(firstId, secondId);
        var reconciler = new ItemReconciler(
            new FixedConfigurationProvider(),
            reader,
            new RejectingWriter());
        using var worker = new ReconciliationWorker(
            reconciler,
            new IncrementalReconciliationOptions(),
            new FullReconcileRequestStore(),
            NullLogger<ReconciliationWorker>.Instance);

        worker.MarkDirty(firstId);
        worker.MarkDirty(secondId);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await reader.FirstStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

        Assert.False(reader.SecondStarted.IsCompleted);
        Assert.Equal(1, worker.Status.RunningItemCount);

        reader.ReleaseFirst();
        await reader.SecondStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(1, reader.MaximumConcurrentReads);
    }

    private sealed class FixedConfigurationProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration _configuration =
            Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
                [
                    new MappingGroupDefinition(
                        new TagNodeDefinition("Target"),
                        [new TagNodeDefinition("Source")],
                        MappingPolicy.Additive,
                        isEnabled: true),
                ]).Configuration);

        public MappingConfiguration? GetConfiguration()
        {
            return _configuration;
        }
    }

    private sealed class BlockingStateReader : IItemStateReader
    {
        private readonly Guid _firstId;
        private readonly Guid _secondId;
        private readonly TaskCompletionSource _firstStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _concurrentReads;

        public BlockingStateReader(Guid firstId, Guid secondId)
        {
            _firstId = firstId;
            _secondId = secondId;
        }

        public Task FirstStarted => _firstStarted.Task;

        public Task SecondStarted => _secondStarted.Task;

        public int MaximumConcurrentReads { get; private set; }

        public void ReleaseFirst()
        {
            _releaseFirst.SetResult();
        }

        public async Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _concurrentReads++;
            MaximumConcurrentReads = Math.Max(MaximumConcurrentReads, _concurrentReads);
            try
            {
                if (itemId == _firstId)
                {
                    _firstStarted.SetResult();
                    await _releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (itemId == _secondId)
                {
                    _secondStarted.SetResult();
                }

                return null;
            }
            finally
            {
                _concurrentReads--;
            }
        }
    }

    private sealed class RejectingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A null state must never produce a plan.");
        }
    }
}
