namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes whether a freshly calculated Full Reconcile plan may apply.
/// </summary>
internal enum FullReconcileSafetyDecision
{
    /// <summary>The fresh plan may apply.</summary>
    Proceed,

    /// <summary>The fresh plan requires a new persisted preview before any write.</summary>
    Paused,
}
