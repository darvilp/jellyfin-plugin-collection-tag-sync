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
                MappingNodeConfigurationMapper.ToDefinition(group.Target),
                (group.Sources ?? []).Select(MappingNodeConfigurationMapper.ToDefinition),
                group.Policy,
                group.IsEnabled);
        }));
    }
}
