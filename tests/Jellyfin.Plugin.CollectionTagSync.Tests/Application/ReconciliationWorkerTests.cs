using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ReconciliationWorkerTests
{
    [Fact]
    public async Task DuplicateInputAndWriterSelfEventProduceOneWriteThenSettledRerun()
    {
        var collectionId = new Guid("b1e224a4-454d-46d0-bf36-ed47fd25e1f5");
        var itemId = new Guid("70ef78ea-a04a-4d2e-9068-59f96c0117a3");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(collectionId, "Animation"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var stateReader = new MutableStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []));
        ReconciliationWorker? worker = null;
        var writer = new SelfEventWriter(plan =>
        {
            stateReader.State = new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                directTags: ["Waltney"],
                directCollectionIds: [collectionId]);
            worker!.MarkDirty(plan.ItemId);
        });
        var reconciler = new ItemReconciler(
            new FixedConfigurationProvider(configuration),
            stateReader,
            writer);
        worker = new ReconciliationWorker(
            reconciler,
            new IncrementalReconciliationOptions(),
            new FullReconcileRequestStore(),
            NullLogger<ReconciliationWorker>.Instance);

        worker.MarkDirty(itemId);
        worker.MarkDirty(itemId);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);

        await stateReader.SecondRead.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);
        worker.Dispose();

        Assert.Equal(2, stateReader.ReadCount);
        Assert.Single(writer.Plans);
        Assert.Equal(
            PlannedMutationKind.AddCollectionMembership,
            Assert.Single(writer.Plans[0].Mutations).Kind);
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

    private sealed class MutableStateReader : IItemStateReader
    {
        private readonly TaskCompletionSource _secondRead = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public MutableStateReader(ObservedItemState state)
        {
            State = state;
        }

        public int ReadCount { get; private set; }

        public Task SecondRead => _secondRead.Task;

        public ObservedItemState State { get; set; }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            if (ReadCount == 2)
            {
                _secondRead.SetResult();
            }

            return Task.FromResult<ObservedItemState?>(State);
        }
    }

    private sealed class SelfEventWriter : IPlanWriter
    {
        private readonly Action<ReconciliationPlan> _onWrite;

        public SelfEventWriter(Action<ReconciliationPlan> onWrite)
        {
            _onWrite = onWrite;
        }

        public List<ReconciliationPlan> Plans { get; } = [];

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            Plans.Add(plan);
            _onWrite(plan);
            return Task.CompletedTask;
        }
    }
}
