using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries one serializer-friendly mapping-shaped operation and ephemeral exclusions.
/// </summary>
public sealed class RunOnceOperationRequest
{
    /// <summary>Gets or sets the one-time target.</summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>Gets or sets the one-time sources.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin API requests require simple settable serializer DTOs.")]
    public MappingNodeConfiguration[] Sources { get; set; } = [];

    /// <summary>Gets or sets the one-time policy.</summary>
    public MappingPolicy Policy { get; set; }

    /// <summary>Gets or sets items that retain their observed direct target state.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin API requests require simple settable serializer DTOs.")]
    public Guid[] ExcludedItemIds { get; set; } = [];
}
