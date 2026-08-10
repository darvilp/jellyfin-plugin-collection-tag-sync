using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one reusable mapping-shaped operation that is never automatic.
/// </summary>
public sealed class RunOnceGroupConfiguration
{
    /// <summary>
    /// Gets or sets the stable persisted group identity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the single target node.
    /// </summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordered source nodes.
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
}
