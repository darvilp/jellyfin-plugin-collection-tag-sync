using System.Diagnostics.CodeAnalysis;
using Jellyfin.Plugin.CollectionTagSync.Application;
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
    /// Gets or sets the monotonically increasing accepted configuration revision.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets the delay after server readiness before startup Full Reconcile is requested.
    /// </summary>
    public int StartupReconcileDelayMinutes { get; set; } = StartupReconcileOptions.DefaultDelayMinutes;

    /// <summary>
    /// Gets or sets a value indicating whether destructive bulk-plan circuit breaking is enabled.
    /// </summary>
    public bool DestructiveCircuitBreakerEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the inclusive maximum number of unique items affected by Authoritative removals.
    /// </summary>
    public int DestructiveMaximumAffectedItems { get; set; } =
        Domain.DestructiveCircuitBreakerOptions.DefaultMaximumAffectedItems;

    /// <summary>
    /// Gets or sets the inclusive maximum per-group removal percentage.
    /// </summary>
    public int DestructiveMaximumRemovalPercentage { get; set; } =
        Domain.DestructiveCircuitBreakerOptions.DefaultMaximumRemovalPercentage;

    /// <summary>
    /// Gets or sets the current-assignment population floor for percentage evaluation.
    /// </summary>
    public int DestructiveMinimumAssignmentPopulation { get; set; } =
        Domain.DestructiveCircuitBreakerOptions.DefaultMinimumAssignmentPopulation;

    /// <summary>
    /// Gets or sets a value indicating whether disabling the circuit breaker was explicitly acknowledged.
    /// </summary>
    public bool DestructiveCircuitBreakerDisableAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets the latest persisted paused Full Reconcile preview diagnostics.
    /// </summary>
    public PausedFullReconcileConfiguration? PausedFullReconcile { get; set; }

    /// <summary>
    /// Gets or sets the persisted continuous mapping groups.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public MappingGroupConfiguration[] MappingGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets reusable run-once groups that are excluded from automatic reconciliation.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin plugin configuration requires simple settable serializer DTOs.")]
    public RunOnceGroupConfiguration[] RunOnceGroups { get; set; } = [];
}
