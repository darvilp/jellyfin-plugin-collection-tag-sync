using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ReconciliationFailureContainmentTests
{
    [Fact]
    public async Task ItemQueuedBeforeQuarantineIsSkippedInsteadOfHotRetried()
    {
        var itemId = new Guid("ee6f9164-5510-4027-8f1f-2af14786c13a");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Target"),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var reader = new CountingNullStateReader();
        using var worker = new ReconciliationWorker(
            new ItemReconciler(
                new FixedConfigurationProvider(configuration),
                reader,
                new RejectingWriter()),
            new IncrementalReconciliationOptions(),
            new FullReconcileRequestStore(),
            NullLogger<ReconciliationWorker>.Instance);
        worker.MarkDirty(itemId);
        ((IFailedItemQuarantine)worker).Quarantine(itemId);

        Assert.Equal(0, worker.Status.QueuedItemCount);
        Assert.Equal(1, worker.Status.QuarantinedItemCount);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await Task.Delay(50).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task ItemQuarantinedWhileWaitingForFullReconcileGateIsNotRetried()
    {
        var itemId = new Guid("f90f97d2-4245-434b-ae84-c7a7b532e205");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Target"),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var reader = new CountingNullStateReader();
        var executionGate = new ReconciliationExecutionGate();
        await executionGate.EnterAsync(CancellationToken.None).ConfigureAwait(true);
        using var worker = new ReconciliationWorker(
            new ItemReconciler(
                new FixedConfigurationProvider(configuration),
                reader,
                new RejectingWriter()),
            new IncrementalReconciliationOptions(),
            new FullReconcileRequestStore(),
            executionGate,
            NullLogger<ReconciliationWorker>.Instance);
        worker.MarkDirty(itemId);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() => worker.Status.RunningItemCount == 1).ConfigureAwait(true);

        ((IFailedItemQuarantine)worker).Quarantine(itemId);
        executionGate.Exit();
        await WaitUntilAsync(() => worker.Status.RunningItemCount == 0).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(1, worker.Status.QuarantinedItemCount);
    }

    [Fact]
    public async Task FailedItemKeepsPartialStateIsQuarantinedAndDoesNotBlockNextItem()
    {
        var firstCollectionId = new Guid("00000000-0000-0000-0000-000000000101");
        var secondCollectionId = new Guid("00000000-0000-0000-0000-000000000202");
        var failedItemId = new Guid("f9fd2aa9-7683-4a05-a01e-f12db7e55c90");
        var successfulItemId = new Guid("55cdd6fe-3e87-43dc-951d-417242ccf78a");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(firstCollectionId, "First"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(secondCollectionId, "Second"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var reader = new MutableMultiItemStateReader(
            failedItemId,
            successfulItemId,
            firstCollectionId,
            secondCollectionId);
        var writer = new PartialFailureWriter(reader, failedItemId);
        ReconciliationWorker? worker = null;
        writer.OnSuccessfulMutation = itemId => worker!.MarkDirty(itemId);
        worker = new ReconciliationWorker(
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new IncrementalReconciliationOptions(),
            new FullReconcileRequestStore(),
            NullLogger<ReconciliationWorker>.Instance);
        using (worker)
        {
            worker.MarkDirty(failedItemId);
            await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
            await writer.FailureReached.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            await WaitUntilAsync(() => worker.Status.QuarantinedItemCount == 1).ConfigureAwait(true);

            worker.MarkDirty(failedItemId);
            worker.MarkDirty(successfulItemId);
            await reader.SuccessfulItemSettled.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(1, reader.GetReadCount(failedItemId));
            Assert.Single(reader.GetCollectionIds(failedItemId));
            Assert.Equal(2, reader.GetCollectionIds(successfulItemId).Count);
            Assert.Equal(1, worker.Status.QuarantinedItemCount);
            Assert.Equal(0, worker.Status.QueuedItemCount);

            worker.CompleteFullReconcile([failedItemId], []);
            worker.MarkDirty(failedItemId);
            Assert.Equal(0, worker.Status.QuarantinedItemCount);
            Assert.Equal(1, worker.Status.QueuedItemCount);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(true);
        }
    }

    private sealed class FixedConfigurationProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration _configuration;

        public FixedConfigurationProvider(MappingConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MappingConfiguration? GetConfiguration()
        {
            return _configuration;
        }
    }

    private sealed class MutableMultiItemStateReader : IItemStateReader
    {
        private readonly Dictionary<Guid, List<Guid>> _collectionIds;
        private readonly Dictionary<Guid, int> _readCounts;
        private readonly Guid _successfulItemId;
        private readonly TaskCompletionSource _successfulItemSettled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public MutableMultiItemStateReader(
            Guid failedItemId,
            Guid successfulItemId,
            Guid firstCollectionId,
            Guid secondCollectionId)
        {
            _successfulItemId = successfulItemId;
            _collectionIds = new Dictionary<Guid, List<Guid>>
            {
                [failedItemId] = [],
                [successfulItemId] = [],
            };
            _readCounts = new Dictionary<Guid, int>
            {
                [failedItemId] = 0,
                [successfulItemId] = 0,
            };
            AllCollectionIds = [firstCollectionId, secondCollectionId];
        }

        public Task SuccessfulItemSettled => _successfulItemSettled.Task;

        public IReadOnlyList<Guid> AllCollectionIds { get; }

        public List<Guid> GetCollectionIds(Guid itemId)
        {
            return _collectionIds[itemId];
        }

        public int GetReadCount(Guid itemId)
        {
            return _readCounts[itemId];
        }

        public void AddCollection(Guid itemId, Guid collectionId)
        {
            _collectionIds[itemId].Add(collectionId);
        }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _readCounts[itemId]++;
            if (itemId == _successfulItemId
                && _collectionIds[itemId].Count == AllCollectionIds.Count)
            {
                _successfulItemSettled.TrySetResult();
            }

            return Task.FromResult<ObservedItemState?>(new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                directTags: ["Waltney"],
                directCollectionIds: _collectionIds[itemId]));
        }
    }

    private sealed class PartialFailureWriter : IPlanWriter
    {
        private readonly Guid _failedItemId;
        private readonly MutableMultiItemStateReader _stateReader;
        private readonly TaskCompletionSource _failureReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public PartialFailureWriter(MutableMultiItemStateReader stateReader, Guid failedItemId)
        {
            _stateReader = stateReader;
            _failedItemId = failedItemId;
        }

        public Action<Guid> OnSuccessfulMutation { get; set; } = _ => { };

        public Task FailureReached => _failureReached.Task;

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            var mutationIndex = 0;
            foreach (var mutation in plan.Mutations)
            {
                if (plan.ItemId == _failedItemId && mutationIndex == 1)
                {
                    _failureReached.TrySetResult();
                    throw new InvalidOperationException("Injected second-operation failure.");
                }

                _stateReader.AddCollection(
                    plan.ItemId,
                    Assert.IsType<CollectionNode>(mutation.Target).Id);
                OnSuccessfulMutation(plan.ItemId);
                mutationIndex++;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingNullStateReader : IItemStateReader
    {
        public int ReadCount { get; private set; }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<ObservedItemState?>(null);
        }
    }

    private sealed class RejectingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A quarantined item must never reach the writer.");
        }
    }
}
