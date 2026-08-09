using Jellyfin.Plugin.CollectionTagSync.Configuration;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Persists accepted plugin configuration through Jellyfin.
/// </summary>
public interface IPluginConfigurationPersistence
{
    /// <summary>
    /// Gets the currently active persisted configuration.
    /// </summary>
    PluginConfiguration Current { get; }

    /// <summary>
    /// Saves one accepted complete configuration.
    /// </summary>
    /// <param name="configuration">The accepted configuration.</param>
    void Save(PluginConfiguration configuration);
}
