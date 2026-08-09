using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ItemReconcilerTests
{
    [Fact]
    public async Task ReconcileReadsPlansAndWritesOnlyWhenDeltaExists()
    {
        var animationId = new Guid("3640a098-40bc-4a25-9ed5-a16666f0fa5c");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(animationId, "Animation"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var stateReader = new StubStateReader(new ObservedItemState(
            new Guid("48444ad0-bff9-4188-bfca-063227e337f1"),
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []));
        var writer = new RecordingWriter();
        var reconciler = new ItemReconciler(
            new FixedConfigurationProvider(configuration),
            stateReader,
            writer);

        var plan = await reconciler
            .ReconcileAsync(stateReader.State.ItemId, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.NotNull(plan);
        Assert.Equal(PlannedMutationKind.AddCollectionMembership, Assert.Single(plan.Mutations).Kind);
        Assert.Same(plan, Assert.Single(writer.Plans));
    }

    [Fact]
    public async Task ReconcileSkipsSecondWriteAfterStateSettles()
    {
        var animationId = new Guid("e9f28a4c-a685-49a0-9e29-afeb8404d944");
        var itemId = new Guid("41742228-a032-4cd3-b121-4343ae8a659d");
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(animationId, "Animation"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var stateReader = new StubStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Series,
            directTags: ["Waltney"],
            directCollectionIds: []));
        var writer = new RecordingWriter();
        var reconciler = new ItemReconciler(
            new FixedConfigurationProvider(configuration),
            stateReader,
            writer);

        await reconciler.ReconcileAsync(itemId, CancellationToken.None).ConfigureAwait(true);
        stateReader.State = new ObservedItemState(
            itemId,
            EligibleItemKind.Series,
            directTags: ["Waltney"],
            directCollectionIds: [animationId]);
        var settledPlan = await reconciler
            .ReconcileAsync(itemId, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Empty(Assert.IsType<ReconciliationPlan>(settledPlan).Mutations);
        Assert.Single(writer.Plans);
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

    private sealed class StubStateReader : IItemStateReader
    {
        public StubStateReader(ObservedItemState state)
        {
            State = state;
        }

        public ObservedItemState State { get; set; }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ObservedItemState?>(State);
        }
    }

    private sealed class RecordingWriter : IPlanWriter
    {
        public List<ReconciliationPlan> Plans { get; } = [];

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }
    }
}
