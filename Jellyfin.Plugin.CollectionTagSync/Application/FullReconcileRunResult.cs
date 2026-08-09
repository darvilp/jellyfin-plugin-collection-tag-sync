using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reports privacy-safe progress or terminal counts for one Full Reconcile.
/// </summary>
public sealed class FullReconcileRunResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileRunResult"/> class.
    /// </summary>
    /// <param name="id">The opaque run identity.</param>
    /// <param name="state">The run state.</param>
    /// <param name="reasons">The coalesced reasons.</param>
    /// <param name="totalItemCount">The total eligible item count.</param>
    /// <param name="succeededItemCount">The successfully processed item count.</param>
    /// <param name="failedItemCount">The failed item count.</param>
    internal FullReconcileRunResult(
        Guid id,
        FullReconcileState state,
        IEnumerable<FullReconcileRequestReason> reasons,
        int totalItemCount,
        int succeededItemCount,
        int failedItemCount)
    {
        Id = id;
        State = state;
        Reasons = Array.AsReadOnly([.. reasons]);
        TotalItemCount = totalItemCount;
        SucceededItemCount = succeededItemCount;
        FailedItemCount = failedItemCount;
    }

    /// <summary>Gets the opaque run identity.</summary>
    public Guid Id { get; }

    /// <summary>Gets the lifecycle state.</summary>
    public FullReconcileState State { get; }

    /// <summary>Gets the reasons coalesced into the run.</summary>
    public IReadOnlyList<FullReconcileRequestReason> Reasons { get; }

    /// <summary>Gets the eligible item count captured for the run.</summary>
    public int TotalItemCount { get; }

    /// <summary>Gets the successfully processed item count.</summary>
    public int SucceededItemCount { get; }

    /// <summary>Gets the contained item failure count.</summary>
    public int FailedItemCount { get; }
}
