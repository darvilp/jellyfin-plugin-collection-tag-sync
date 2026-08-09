using System;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one serializer-friendly tag or collection node.
/// </summary>
public sealed class MappingNodeConfiguration
{
    /// <summary>
    /// Gets or sets the node kind.
    /// </summary>
    public MappingNodeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the configured tag spelling.
    /// </summary>
    public string TagValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin collection identifier.
    /// </summary>
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the administrator-facing collection display name.
    /// </summary>
    public string CollectionDisplayName { get; set; } = string.Empty;
}
