using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Validates and resolves persisted destructive circuit-breaker settings.
/// </summary>
internal static class DestructiveCircuitBreakerConfiguration
{
    /// <summary>
    /// Determines whether all configured safety limits preserve accepted boundaries.
    /// </summary>
    /// <param name="configuration">The candidate persisted configuration.</param>
    /// <returns><see langword="true"/> when every limit is valid.</returns>
    public static bool HasValidLimits(PluginConfiguration configuration)
    {
        return DestructiveCircuitBreakerOptions.IsValidMaximumAffectedItems(
                configuration.DestructiveMaximumAffectedItems)
            && DestructiveCircuitBreakerOptions.IsValidMaximumRemovalPercentage(
                configuration.DestructiveMaximumRemovalPercentage)
            && DestructiveCircuitBreakerOptions.IsValidMinimumAssignmentPopulation(
                configuration.DestructiveMinimumAssignmentPopulation);
    }

    /// <summary>
    /// Creates fail-safe immutable options from one persisted configuration.
    /// </summary>
    /// <param name="configuration">The active persisted configuration.</param>
    /// <returns>Validated options with accepted defaults substituted for invalid legacy values.</returns>
    public static DestructiveCircuitBreakerOptions CreateOptions(PluginConfiguration configuration)
    {
        var enabled = configuration.DestructiveCircuitBreakerEnabled
            || !configuration.DestructiveCircuitBreakerDisableAcknowledged;
        var maximumAffectedItems = DestructiveCircuitBreakerOptions.IsValidMaximumAffectedItems(
            configuration.DestructiveMaximumAffectedItems)
            ? configuration.DestructiveMaximumAffectedItems
            : DestructiveCircuitBreakerOptions.DefaultMaximumAffectedItems;
        var maximumRemovalPercentage = DestructiveCircuitBreakerOptions.IsValidMaximumRemovalPercentage(
            configuration.DestructiveMaximumRemovalPercentage)
            ? configuration.DestructiveMaximumRemovalPercentage
            : DestructiveCircuitBreakerOptions.DefaultMaximumRemovalPercentage;
        var minimumAssignmentPopulation = DestructiveCircuitBreakerOptions.IsValidMinimumAssignmentPopulation(
            configuration.DestructiveMinimumAssignmentPopulation)
            ? configuration.DestructiveMinimumAssignmentPopulation
            : DestructiveCircuitBreakerOptions.DefaultMinimumAssignmentPopulation;
        return new DestructiveCircuitBreakerOptions(
            enabled,
            maximumAffectedItems,
            maximumRemovalPercentage,
            minimumAssignmentPopulation);
    }
}
