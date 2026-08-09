using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Coalesces reasons that require a future serialized Full Reconcile.
/// </summary>
public sealed class FullReconcileRequestStore
{
    private readonly object _sync = new();
    private readonly HashSet<FullReconcileRequestReason> _reasons = [];

    /// <summary>
    /// Gets the current coalesced request status.
    /// </summary>
    public FullReconcileRequestStatus Status
    {
        get
        {
            lock (_sync)
            {
                return new FullReconcileRequestStatus(_reasons.Order());
            }
        }
    }

    /// <summary>
    /// Adds one reason to the coalesced request.
    /// </summary>
    /// <param name="reason">The request reason.</param>
    internal void Request(FullReconcileRequestReason reason)
    {
        lock (_sync)
        {
            _reasons.Add(reason);
        }
    }

    /// <summary>
    /// Clears fulfilled request reasons.
    /// </summary>
    internal void Clear()
    {
        lock (_sync)
        {
            _reasons.Clear();
        }
    }
}
