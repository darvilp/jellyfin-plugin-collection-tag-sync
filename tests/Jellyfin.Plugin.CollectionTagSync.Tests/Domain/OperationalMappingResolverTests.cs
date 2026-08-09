using System;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class OperationalMappingResolverTests
{
    [Fact]
    public void MissingMixedSourceDisablesWholeGroupAndPassesObservedTargetDownstream()
    {
        var missingSourceId = new Guid("37ca0db7-3bed-498d-94c9-845234491301");
        var downstreamId = new Guid("d12bc919-f39c-49ce-82a2-623524569de3");
        var itemId = new Guid("661fcfd0-600a-46e6-95a1-563188539185");
        var configured = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Kid-Approved"),
                    [
                        new CollectionNodeDefinition(missingSourceId, "Missing"),
                        new TagNodeDefinition("Waltney"),
                    ],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(downstreamId, "Kids"),
                    [new TagNodeDefinition("Kid-Approved")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);

        var operational = OperationalMappingResolver.Resolve(configured, [downstreamId]);
        var plan = ReconciliationPlanner.Plan(
            operational.Configuration,
            new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                directTags: ["Kid-Approved"],
                directCollectionIds: []));

        var diagnostic = Assert.Single(operational.UnresolvedGroups);
        Assert.Equal(0, diagnostic.GroupIndex);
        Assert.Equal(missingSourceId, Assert.Single(diagnostic.MissingCollections).Id);
        Assert.False(operational.Configuration.Groups[0].IsEnabled);
        Assert.True(operational.Configuration.Groups[1].IsEnabled);
        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.AddCollectionMembership, mutation.Kind);
        Assert.Equal(downstreamId, Assert.IsType<CollectionNode>(mutation.Target).Id);
    }

    [Fact]
    public void DisabledGroupDoesNotReportMissingCollection()
    {
        var missingId = new Guid("cbe845bf-26a7-4a05-ad91-391b37b0f0ec");
        var configured = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Kid-Approved"),
                    [new CollectionNodeDefinition(missingId, "Missing")],
                    MappingPolicy.Additive,
                    isEnabled: false),
            ]).Configuration);

        var operational = OperationalMappingResolver.Resolve(configured, []);

        Assert.Empty(operational.UnresolvedGroups);
        Assert.False(operational.Configuration.Groups[0].IsEnabled);
    }

    [Fact]
    public void MissingTargetFailsClosedWithoutChangingConfiguredEnabledState()
    {
        var missingTargetId = new Guid("695ec755-1fd4-447d-9518-7cf41da03a71");
        var itemId = new Guid("ec835ea3-18da-4848-b3d2-1ef67bcedc71");
        var configured = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(missingTargetId, "Deleted target"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
            ]).Configuration);

        var operational = OperationalMappingResolver.Resolve(configured, []);
        var plan = ReconciliationPlanner.Plan(
            operational.Configuration,
            new ObservedItemState(
                itemId,
                EligibleItemKind.Series,
                directTags: ["Waltney"],
                directCollectionIds: []));

        Assert.True(configured.Groups[0].IsEnabled);
        Assert.False(operational.Configuration.Groups[0].IsEnabled);
        Assert.Equal(missingTargetId, Assert.Single(Assert.Single(operational.UnresolvedGroups).MissingCollections).Id);
        Assert.Empty(plan.Mutations);
    }
}
