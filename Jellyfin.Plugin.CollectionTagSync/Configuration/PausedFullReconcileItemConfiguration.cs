using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one item's complete non-executable planner diagnostics.
/// </summary>
public sealed class PausedFullReconcileItemConfiguration
{
    /// <summary>Gets or sets the eligible item identifier.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the current Jellyfin item title for API display.</summary>
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the eligible item kind.</summary>
    public EligibleItemKind ItemKind { get; set; }

    /// <summary>Gets or sets the planned direct additions and removals.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public PausedFullReconcileMutationConfiguration[] Mutations { get; set; } = [];

    /// <summary>Gets or sets the planner's final settled target evaluations.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public PausedFullReconcileTargetEvaluationConfiguration[] TargetEvaluations { get; set; } = [];
}
