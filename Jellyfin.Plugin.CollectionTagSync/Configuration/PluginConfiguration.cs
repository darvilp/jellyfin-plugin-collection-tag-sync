using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Persisted plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the persisted configuration schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
}
