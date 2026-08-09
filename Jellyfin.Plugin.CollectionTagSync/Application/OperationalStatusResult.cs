using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Aggregates privacy-safe coordinator and unresolved-reference status for the UI.
/// </summary>
public sealed class OperationalStatusResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationalStatusResult"/> class.
    /// </summary>
    /// <param name="incremental">The incremental coordinator state.</param>
    /// <param name="fullReconcileRequest">The pending broader-recovery request.</param>
    /// <param name="unresolvedGroups">The current unresolved mapping groups.</param>
    internal OperationalStatusResult(
        IncrementalReconciliationStatus incremental,
        FullReconcileRequestStatus fullReconcileRequest,
        IEnumerable<UnresolvedMappingGroupStatus> unresolvedGroups)
    {
        Incremental = incremental;
        FullReconcileRequest = fullReconcileRequest;
        UnresolvedGroups = [.. unresolvedGroups];
    }

    /// <summary>Gets the incremental coordinator state.</summary>
    public IncrementalReconciliationStatus Incremental { get; }

    /// <summary>Gets pending Full Reconcile demand.</summary>
    public FullReconcileRequestStatus FullReconcileRequest { get; }

    /// <summary>Gets unresolved enabled mapping groups.</summary>
    public IReadOnlyList<UnresolvedMappingGroupStatus> UnresolvedGroups { get; }
}
