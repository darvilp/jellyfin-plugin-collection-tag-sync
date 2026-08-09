using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Accepts item identities that require desired-state reconciliation.
/// </summary>
public interface IDirtyItemSink
{
    /// <summary>
    /// Marks one item for reconciliation.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    void MarkDirty(Guid itemId);
}
