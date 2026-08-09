using System;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests;

public sealed class PluginConfigurationContractTests
{
    [Fact]
    public void NewConfigurationStartsAtSchemaVersionOne()
    {
        var configuration = new PluginConfiguration();

        Assert.Equal(1, configuration.SchemaVersion);
    }

    [Fact]
    public void WalkingSliceStartsDisabledAndUnbound()
    {
        var configuration = new PluginConfiguration();

        Assert.False(configuration.WalkingSliceEnabled);
        Assert.Equal(string.Empty, configuration.WalkingSliceSourceTag);
        Assert.Equal(Guid.Empty, configuration.WalkingSliceTargetCollectionId);
        Assert.Equal(string.Empty, configuration.WalkingSliceTargetCollectionName);
    }
}
