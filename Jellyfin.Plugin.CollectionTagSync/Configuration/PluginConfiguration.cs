using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Gets or sets the monotonically increasing accepted configuration revision.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets the persisted continuous mapping groups.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public MappingGroupConfiguration[] MappingGroups { get; set; } = [];
}
