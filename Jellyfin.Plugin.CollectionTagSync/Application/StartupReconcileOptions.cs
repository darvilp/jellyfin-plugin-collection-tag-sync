namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Defines the persisted startup Full Reconcile delay contract.
/// </summary>
public static class StartupReconcileOptions
{
    /// <summary>
    /// The default startup delay in minutes.
    /// </summary>
    public const int DefaultDelayMinutes = 5;

    /// <summary>
    /// The largest supported startup delay in minutes.
    /// </summary>
    public const int MaximumDelayMinutes = 60;

    /// <summary>
    /// Determines whether a configured delay is inside the supported inclusive range.
    /// </summary>
    /// <param name="delayMinutes">The proposed delay in minutes.</param>
    /// <returns><see langword="true"/> when the delay is valid.</returns>
    public static bool IsValidDelay(int delayMinutes)
    {
        return delayMinutes is >= 0 and <= MaximumDelayMinutes;
    }
}
