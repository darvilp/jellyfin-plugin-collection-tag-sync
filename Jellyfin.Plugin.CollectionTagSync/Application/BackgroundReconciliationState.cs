namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the lifecycle state of a background reconciliation request.
/// </summary>
public enum BackgroundReconciliationState
{
    /// <summary>
    /// The request is accepted and waiting for the serialized worker.
    /// </summary>
    Queued,

    /// <summary>
    /// The request currently owns the serialization boundary.
    /// </summary>
    Running,

    /// <summary>
    /// Every item completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Some items completed and some failed.
    /// </summary>
    PartiallyFailed,

    /// <summary>
    /// Every attempted item failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Execution requires administrator preview and authorization.
    /// </summary>
    Paused,
}
