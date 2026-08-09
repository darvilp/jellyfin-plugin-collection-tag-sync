namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the privacy-safe lifecycle of the current or latest Full Reconcile.
/// </summary>
public enum FullReconcileState
{
    /// <summary>No run has started in this process.</summary>
    Idle,

    /// <summary>The run is calculating a complete plan.</summary>
    Planning,

    /// <summary>The run is applying its calculated plans.</summary>
    Applying,

    /// <summary>The run completed without item failures.</summary>
    Completed,

    /// <summary>The run completed after containing one or more item failures.</summary>
    CompletedWithFailures,

    /// <summary>The run could not reach item-level processing.</summary>
    Failed,
}
