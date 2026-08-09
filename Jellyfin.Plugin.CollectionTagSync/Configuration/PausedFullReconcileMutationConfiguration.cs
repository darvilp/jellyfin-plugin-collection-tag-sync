using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one non-executable direct mutation preview.
/// </summary>
public sealed class PausedFullReconcileMutationConfiguration
{
    /// <summary>Gets or sets the direct mutation kind.</summary>
    public PlannedMutationKind Kind { get; set; }

    /// <summary>Gets or sets the normalized target identity.</summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>Gets or sets the target policy.</summary>
    public MappingPolicy Policy { get; set; }

    /// <summary>Gets or sets the effective sources supporting the target.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public MappingNodeConfiguration[] SupportingSources { get; set; } = [];

    /// <summary>Gets or sets the exact tag spellings added or removed.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public string[] TagValues { get; set; } = [];
}
