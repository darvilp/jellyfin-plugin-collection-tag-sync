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
    /// Releases failed-item quarantine and storm fallback after Full Reconcile completes.
    /// </summary>
    void ResetAfterFullReconcile();
}
