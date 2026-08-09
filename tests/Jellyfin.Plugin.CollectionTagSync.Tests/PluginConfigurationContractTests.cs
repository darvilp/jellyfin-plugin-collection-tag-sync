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
}
