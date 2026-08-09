using System;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one exact normalized removal tuple for a paused Full Reconcile preview.
/// </summary>
public sealed class PausedFullReconcileRemovalConfiguration
{
    /// <summary>Gets or sets the eligible item identifier.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the normalized target identity.</summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>Gets or sets the direct removal kind.</summary>
    public PlannedMutationKind Kind { get; set; }
}
