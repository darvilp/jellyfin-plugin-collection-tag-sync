using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Exposes privacy-safe incremental state and the Full Reconcile recovery boundary.
/// </summary>
public interface IIncrementalReconciliationControl
{
    /// <summary>
    /// Gets the current incremental coordinator status.
    /// </summary>
    IncrementalReconciliationStatus Status { get; }

    /// <summary>
    /// Releases repaired items, retains newly failed items, and clears storm fallback after Full Reconcile.
    /// </summary>
    /// <param name="repairedItemIds">Items successfully evaluated and applied by the run.</param>
    /// <param name="failedItemIds">Items whose planning or writing failed during the run.</param>
    void CompleteFullReconcile(
        IEnumerable<Guid> repairedItemIds,
        IEnumerable<Guid> failedItemIds);
}
