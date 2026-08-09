using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ReconciliationStormTests
{
    [Fact]
    public async Task PendingLimitRequestsFullReconcileAndStopsQueueGrowth()
    {
        var firstId = new Guid("fd3527f1-e366-48b7-a564-17e5ca5f2feb");
        var secondId = new Guid("55d2470a-a011-46e5-a4c7-3dd0979559a8");
        var excludedId = new Guid("f5a8b3d7-0072-47b5-94b4-11856be82d45");
        var laterId = new Guid("10213d7b-a9ef-4d02-bf31-815b818ee914");
        var reader = new NullStateReader(expectedReadCount: 2);
        var fullReconcileRequests = new FullReconcileRequestStore();
        using var worker = CreateWorker(reader, fullReconcileRequests, maxPendingItems: 2);

        worker.MarkDirty(firstId);
        worker.MarkDirty(firstId);
        worker.MarkDirty(secondId);
        worker.MarkDirty(excludedId);

        Assert.Equal(2, worker.Status.QueuedItemCount);
        Assert.True(worker.Status.IsStormFallbackActive);
        Assert.True(fullReconcileRequests.Status.IsRequested);
        Assert.Contains(FullReconcileRequestReason.EventStorm, fullReconcileRequests.Status.Reasons);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await reader.ExpectedReads.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        worker.MarkDirty(laterId);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { firstId, secondId }, reader.ReadItemIds);
        Assert.Equal(0, worker.Status.QueuedItemCount);
        Assert.True(worker.Status.IsStormFallbackActive);

        worker.ResetAfterFullReconcile();
        worker.MarkDirty(laterId);
        Assert.False(worker.Status.IsStormFallbackActive);
        Assert.Equal(1, worker.Status.QueuedItemCount);
        Assert.False(fullReconcileRequests.Status.IsRequested);
    }

    private static ReconciliationWorker CreateWorker(
        IItemStateReader reader,
        FullReconcileRequestStore requestStore,
        int maxPendingItems)
    {
        return new ReconciliationWorker(
            new ItemReconciler(new FixedConfigurationProvider(), reader, new RejectingWriter()),
            new IncrementalReconciliationOptions(maxPendingItems),
            requestStore,
            NullLogger<ReconciliationWorker>.Instance);
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

    private sealed class NullStateReader : IItemStateReader
    {
        private readonly int _expectedReadCount;
        private readonly TaskCompletionSource _expectedReads = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public NullStateReader(int expectedReadCount)
        {
            _expectedReadCount = expectedReadCount;
        }

        public Task ExpectedReads => _expectedReads.Task;

        public List<Guid> ReadItemIds { get; } = [];

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            ReadItemIds.Add(itemId);
            if (ReadItemIds.Count == _expectedReadCount)
            {
                _expectedReads.SetResult();
            }

            return Task.FromResult<ObservedItemState?>(null);
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
