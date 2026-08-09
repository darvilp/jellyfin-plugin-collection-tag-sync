using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Quarantines failed items from later incremental hot retries.
/// </summary>
internal interface IFailedItemQuarantine
{
    /// <summary>
    /// Quarantines one item until Full Reconcile resets recovery state.
    /// </summary>
    /// <param name="itemId">The failed item identity.</param>
    void Quarantine(Guid itemId);
}
