namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies why a coalesced Full Reconcile is required.
/// </summary>
public enum FullReconcileRequestReason
{
    /// <summary>
    /// Jellyfin invoked the scheduled task manually or from an administrator-configured schedule.
    /// </summary>
    Manual,

    /// <summary>
    /// Server startup requested delayed recovery for enabled mappings.
    /// </summary>
    Startup,

    /// <summary>
    /// Fine-grained event activity exceeded the bounded dirty-item set.
    /// </summary>
    EventStorm,
}
