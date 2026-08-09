namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides a privacy-safe snapshot of incremental coordinator state.
/// </summary>
public sealed class IncrementalReconciliationStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalReconciliationStatus"/> class.
    /// </summary>
    /// <param name="queuedItemCount">The unique pending item count.</param>
    /// <param name="runningItemCount">The running item count.</param>
    /// <param name="quarantinedItemCount">The failed item count.</param>
    /// <param name="isStormFallbackActive">Whether fine-grained queue growth is stopped.</param>
    internal IncrementalReconciliationStatus(
        int queuedItemCount,
        int runningItemCount,
        int quarantinedItemCount,
        bool isStormFallbackActive)
    {
        QueuedItemCount = queuedItemCount;
        RunningItemCount = runningItemCount;
        QuarantinedItemCount = quarantinedItemCount;
        IsStormFallbackActive = isStormFallbackActive;
    }

    /// <summary>
    /// Gets the number of unique pending item identities.
    /// </summary>
    public int QueuedItemCount { get; }

    /// <summary>
    /// Gets the number of items currently being reconciled.
    /// </summary>
    public int RunningItemCount { get; }

    /// <summary>
    /// Gets the number of failed items deferred until Full Reconcile.
    /// </summary>
    public int QuarantinedItemCount { get; }

    /// <summary>
    /// Gets a value indicating whether event-storm fallback is active.
    /// </summary>
    public bool IsStormFallbackActive { get; }
}
