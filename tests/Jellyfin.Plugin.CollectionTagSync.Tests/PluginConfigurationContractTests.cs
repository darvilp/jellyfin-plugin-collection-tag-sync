using Jellyfin.Plugin.CollectionTagSync.Application;
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
        Assert.Equal(0, configuration.Revision);
    }

    [Fact]
    public void ContinuousMappingsStartEmpty()
    {
        var configuration = new PluginConfiguration();

        Assert.Empty(configuration.MappingGroups);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(60)]
    public void StartupReconcileDelayAcceptsDocumentedBounds(int delayMinutes)
    {
        Assert.True(StartupReconcileOptions.IsValidDelay(delayMinutes));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void StartupReconcileDelayRejectsValuesOutsideDocumentedBounds(int delayMinutes)
    {
        Assert.False(StartupReconcileOptions.IsValidDelay(delayMinutes));
    }

    [Fact]
    public void StartupReconcileDelayDefaultsToFiveMinutes()
    {
        var configuration = new PluginConfiguration();

        Assert.Equal(5, configuration.StartupReconcileDelayMinutes);
    }
}
