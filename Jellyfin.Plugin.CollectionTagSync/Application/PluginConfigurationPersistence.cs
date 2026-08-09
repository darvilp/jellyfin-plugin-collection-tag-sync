using Jellyfin.Plugin.CollectionTagSync.Configuration;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Persists accepted configuration through the loaded Jellyfin plugin instance.
/// </summary>
internal sealed class PluginConfigurationPersistence : IPluginConfigurationPersistence
{
    /// <inheritdoc />
    public PluginConfiguration Current => Plugin.Instance.Configuration;

    /// <inheritdoc />
    public void Save(PluginConfiguration configuration)
    {
        Plugin.Instance.ActivateValidatedConfiguration(configuration);
    }
}
