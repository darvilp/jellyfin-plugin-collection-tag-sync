using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
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

    [Fact]
    public void SavedRunOnceGroupsStartEmpty()
    {
        var configuration = new PluginConfiguration();

        Assert.Empty(configuration.RunOnceGroups);
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

    [Fact]
    public void DestructiveCircuitBreakerDefaultsMatchAcceptedBoundaries()
    {
        var configuration = new PluginConfiguration();

        Assert.True(configuration.DestructiveCircuitBreakerEnabled);
        Assert.Equal(25, configuration.DestructiveMaximumAffectedItems);
        Assert.Equal(20, configuration.DestructiveMaximumRemovalPercentage);
        Assert.Equal(10, configuration.DestructiveMinimumAssignmentPopulation);
        Assert.False(configuration.DestructiveCircuitBreakerDisableAcknowledged);
    }

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(25, 20, 10)]
    [InlineData(100, 100, 100)]
    public void DestructiveCircuitBreakerLimitsAcceptDocumentedBoundaries(
        int maximumItems,
        int maximumPercentage,
        int populationFloor)
    {
        Assert.True(DestructiveCircuitBreakerOptions.IsValidMaximumAffectedItems(maximumItems));
        Assert.True(DestructiveCircuitBreakerOptions.IsValidMaximumRemovalPercentage(maximumPercentage));
        Assert.True(DestructiveCircuitBreakerOptions.IsValidMinimumAssignmentPopulation(populationFloor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void DestructivePercentagePopulationFloorCannotWeakenAcceptedMinimum(int populationFloor)
    {
        Assert.False(DestructiveCircuitBreakerOptions.IsValidMinimumAssignmentPopulation(populationFloor));
    }
}
