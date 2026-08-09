using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Builds the bounded Phase 3 mapping from persisted plugin configuration.
/// </summary>
internal sealed class WalkingSliceMappingProvider : IActiveMappingProvider
{
    /// <inheritdoc />
    public MappingConfiguration? GetConfiguration()
    {
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.WalkingSliceEnabled)
        {
            return null;
        }

        return MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(
                        configuration.WalkingSliceTargetCollectionId,
                        configuration.WalkingSliceTargetCollectionName),
                    [new TagNodeDefinition(configuration.WalkingSliceSourceTag)],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration;
    }
}
