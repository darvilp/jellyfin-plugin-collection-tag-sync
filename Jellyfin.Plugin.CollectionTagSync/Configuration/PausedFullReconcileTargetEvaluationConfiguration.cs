using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one mapped target's observed and final settled preview state.
/// </summary>
public sealed class PausedFullReconcileTargetEvaluationConfiguration
{
    /// <summary>Gets or sets the normalized target identity.</summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>Gets or sets the target policy.</summary>
    public MappingPolicy Policy { get; set; }

    /// <summary>Gets or sets a value indicating whether the target is directly observed.</summary>
    public bool ObservedState { get; set; }

    /// <summary>Gets or sets a value indicating whether the target is present in the final settled state.</summary>
    public bool EffectiveState { get; set; }

    /// <summary>Gets or sets the effective sources supporting the target.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public MappingNodeConfiguration[] SupportingSources { get; set; } = [];
}
