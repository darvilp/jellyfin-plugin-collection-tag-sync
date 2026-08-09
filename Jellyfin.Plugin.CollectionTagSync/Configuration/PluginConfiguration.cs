using System;
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
    /// Gets or sets a value indicating whether the bounded Phase 3 walking slice is enabled.
    /// </summary>
    public bool WalkingSliceEnabled { get; set; }

    /// <summary>
    /// Gets or sets the bounded walking-slice source tag.
    /// </summary>
    public string WalkingSliceSourceTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bounded walking-slice target collection identifier.
    /// </summary>
    public Guid WalkingSliceTargetCollectionId { get; set; }

    /// <summary>
    /// Gets or sets the target collection display name used in diagnostics.
    /// </summary>
    public string WalkingSliceTargetCollectionName { get; set; } = string.Empty;
}
