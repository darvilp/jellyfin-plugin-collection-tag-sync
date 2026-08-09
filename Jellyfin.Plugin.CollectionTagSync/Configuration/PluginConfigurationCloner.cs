using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Creates independent serializer-friendly plugin configuration snapshots.
/// </summary>
internal static class PluginConfigurationCloner
{
    /// <summary>
    /// Clones one complete persisted configuration snapshot.
    /// </summary>
    /// <param name="source">The source snapshot.</param>
    /// <returns>An independent deep clone.</returns>
    public static PluginConfiguration Clone(PluginConfiguration source)
    {
        return new PluginConfiguration
        {
            SchemaVersion = source.SchemaVersion,
            Revision = source.Revision,
            StartupReconcileDelayMinutes = source.StartupReconcileDelayMinutes,
            DestructiveCircuitBreakerEnabled = source.DestructiveCircuitBreakerEnabled,
            DestructiveMaximumAffectedItems = source.DestructiveMaximumAffectedItems,
            DestructiveMaximumRemovalPercentage = source.DestructiveMaximumRemovalPercentage,
            DestructiveMinimumAssignmentPopulation = source.DestructiveMinimumAssignmentPopulation,
            DestructiveCircuitBreakerDisableAcknowledged = source.DestructiveCircuitBreakerDisableAcknowledged,
            PausedFullReconcile = PausedFullReconcileConfigurationMapper.Clone(source.PausedFullReconcile),
            MappingGroups = (source.MappingGroups ?? [])
                .Where(group => group is not null)
                .Select(CloneGroup)
                .ToArray(),
        };
    }

    private static MappingGroupConfiguration CloneGroup(MappingGroupConfiguration group)
    {
        return new MappingGroupConfiguration
        {
            Target = CloneNode(group.Target),
            Sources = (group.Sources ?? []).Select(CloneNode).ToArray(),
            Policy = group.Policy,
            IsEnabled = group.IsEnabled,
        };
    }

    private static MappingNodeConfiguration CloneNode(MappingNodeConfiguration? node)
    {
        node ??= new MappingNodeConfiguration();
        return new MappingNodeConfiguration
        {
            Kind = node.Kind,
            TagValue = node.TagValue,
            CollectionId = node.CollectionId,
            CollectionDisplayName = node.CollectionDisplayName,
        };
    }
}
