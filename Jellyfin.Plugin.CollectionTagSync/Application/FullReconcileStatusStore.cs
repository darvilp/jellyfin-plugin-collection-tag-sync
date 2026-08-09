using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Retains the current or latest privacy-safe Full Reconcile status.
/// </summary>
public sealed class FullReconcileStatusStore
{
    private readonly object _sync = new();
    private FullReconcileRunResult _current = new(
        Guid.Empty,
        FullReconcileState.Idle,
        [],
        totalItemCount: 0,
        succeededItemCount: 0,
        failedItemCount: 0);

    /// <summary>
    /// Gets the current immutable status snapshot.
    /// </summary>
    public FullReconcileRunResult Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Replaces the current run snapshot.
    /// </summary>
    /// <param name="status">The immutable replacement snapshot.</param>
    internal void Update(FullReconcileRunResult status)
    {
        ArgumentNullException.ThrowIfNull(status);

        lock (_sync)
        {
            _current = status;
        }
    }
}
