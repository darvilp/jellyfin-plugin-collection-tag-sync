using System;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Converts persisted mutable DTOs into the validated immutable domain model.
/// </summary>
public static class PluginConfigurationMapper
{
    /// <summary>
    /// Validates and maps a complete persisted plugin configuration.
    /// </summary>
    /// <param name="configuration">The persisted configuration.</param>
    /// <returns>The validated domain configuration or validation diagnostics.</returns>
    public static MappingConfigurationValidationResult ToDomain(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return MappingConfiguration.Create((configuration.MappingGroups ?? []).Select(group =>
        {
            group ??= new MappingGroupConfiguration();
            return new MappingGroupDefinition(
                ToDefinition(group.Target ?? new MappingNodeConfiguration()),
                (group.Sources ?? []).Select(ToDefinition),
                group.Policy,
                group.IsEnabled);
        }));
    }

    private static NodeDefinition ToDefinition(MappingNodeConfiguration node)
    {
        return node.Kind switch
        {
            MappingNodeKind.Tag => new TagNodeDefinition(node.TagValue),
            MappingNodeKind.Collection => new CollectionNodeDefinition(
                node.CollectionId,
                node.CollectionDisplayName),
            _ => new TagNodeDefinition(null),
        };
    }
}
