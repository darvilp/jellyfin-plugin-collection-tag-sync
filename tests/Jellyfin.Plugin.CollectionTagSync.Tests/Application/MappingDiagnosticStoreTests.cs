using System;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class MappingDiagnosticStoreTests
{
    [Fact]
    public void RetainsUnresolvedWarningUntilRepairedOrDisabledSnapshotReplacesIt()
    {
        var missingId = new Guid("4e6255a0-2dd2-4c06-be8a-c16138337f30");
        var configured = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Kid-Approved"),
                    [new CollectionNodeDefinition(missingId, "Missing")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
            ]).Configuration);
        var unresolved = OperationalMappingResolver.Resolve(configured, []).UnresolvedGroups;
        var store = new MappingDiagnosticStore();

        Assert.True(store.Update(unresolved));
        Assert.False(store.Update(unresolved));
        Assert.Equal(missingId, Assert.Single(Assert.Single(store.UnresolvedGroups).MissingCollections).Id);

        var renamedConfiguration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Kid-Approved"),
                    [new CollectionNodeDefinition(missingId, "Renamed missing display")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
            ]).Configuration);
        var renamedDiagnostic = OperationalMappingResolver.Resolve(renamedConfiguration, []).UnresolvedGroups;
        Assert.True(store.Update(renamedDiagnostic));
        Assert.Equal(
            "Renamed missing display",
            Assert.Single(Assert.Single(store.UnresolvedGroups).MissingCollections).DisplayName);

        Assert.True(store.Update([]));
        Assert.Empty(store.UnresolvedGroups);
    }
}
