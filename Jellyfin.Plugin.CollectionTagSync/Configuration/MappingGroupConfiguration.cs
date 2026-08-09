using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one serializer-friendly continuous mapping group.
/// </summary>
public sealed class MappingGroupConfiguration
{
    /// <summary>
    /// Gets or sets the single target node.
    /// </summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>
    /// Gets or sets the source nodes.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public MappingNodeConfiguration[] Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the mapping policy.
    /// </summary>
    public MappingPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
