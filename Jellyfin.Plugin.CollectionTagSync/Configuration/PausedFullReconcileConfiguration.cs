using System;
using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Application;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores non-executable diagnostics for the latest paused Full Reconcile.
/// </summary>
public sealed class PausedFullReconcileConfiguration
{
    /// <summary>Gets or sets the opaque paused-run identity.</summary>
    public Guid RunId { get; set; }

    /// <summary>Gets or sets the active configuration revision used for planning.</summary>
    public long ConfigurationRevision { get; set; }

    /// <summary>Gets or sets the UTC diagnostic creation time.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>Gets or sets the coalesced run reasons.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public FullReconcileRequestReason[] Reasons { get; set; } = [];

    /// <summary>Gets or sets the total eligible-item count.</summary>
    public int TotalItemCount { get; set; }

    /// <summary>Gets or sets the unique affected-item count.</summary>
    public int UniqueAffectedItemCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the absolute limit was exceeded.</summary>
    public bool ExceedsAbsoluteLimit { get; set; }

    /// <summary>Gets or sets exact normalized Authoritative removal diagnostics.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public PausedFullReconcileRemovalConfiguration[] Removals { get; set; } = [];

    /// <summary>Gets or sets per-group threshold diagnostics.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public PausedFullReconcileGroupConfiguration[] Groups { get; set; } = [];

    /// <summary>Gets or sets complete item-level planner diagnostics.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public PausedFullReconcileItemConfiguration[] Items { get; set; } = [];
}
