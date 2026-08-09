namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies why a coalesced Full Reconcile is required.
/// </summary>
public enum FullReconcileRequestReason
{
    /// <summary>
    /// Fine-grained event activity exceeded the bounded dirty-item set.
    /// </summary>
    EventStorm,
}
