using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Retains privacy-safe background reconciliation status in memory.
/// </summary>
public sealed class BackgroundReconciliationStatusStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, BackgroundReconciliationStatus> _statuses = [];

    /// <summary>
    /// Gets the number of retained request statuses.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _statuses.Count;
            }
        }
    }

    /// <summary>
    /// Gets one request status.
    /// </summary>
    /// <param name="id">The opaque request identity.</param>
    /// <returns>The status, or <see langword="null"/> when unknown.</returns>
    public BackgroundReconciliationStatus? Get(Guid id)
    {
        lock (_sync)
        {
            return _statuses.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Creates one queued status.
    /// </summary>
    /// <param name="revision">The accepted configuration revision.</param>
    /// <param name="totalItemCount">The eligible item count.</param>
    /// <returns>The opaque request identity.</returns>
    internal Guid CreateQueued(long revision, int totalItemCount)
    {
        return Create(revision, BackgroundReconciliationState.Queued, totalItemCount);
    }

    /// <summary>
    /// Creates one paused status for a removal-bearing candidate.
    /// </summary>
    /// <param name="revision">The still-active configuration revision.</param>
    /// <param name="totalItemCount">The evaluated item count.</param>
    /// <returns>The opaque request identity.</returns>
    internal Guid CreatePaused(long revision, int totalItemCount)
    {
        return Create(revision, BackgroundReconciliationState.Paused, totalItemCount);
    }

    /// <summary>
    /// Marks one queued request running.
    /// </summary>
    /// <param name="id">The request identity.</param>
    internal void MarkRunning(Guid id)
    {
        Update(id, status => new BackgroundReconciliationStatus(
            status.Id,
            status.ConfigurationRevision,
            BackgroundReconciliationState.Running,
            status.TotalItemCount,
            status.CompletedItemCount,
            status.FailedItemCount));
    }

    /// <summary>
    /// Records one successfully settled item.
    /// </summary>
    /// <param name="id">The request identity.</param>
    internal void RecordSuccess(Guid id)
    {
        Update(id, status => new BackgroundReconciliationStatus(
            status.Id,
            status.ConfigurationRevision,
            status.State,
            status.TotalItemCount,
            checked(status.CompletedItemCount + 1),
            status.FailedItemCount));
    }

    /// <summary>
    /// Records one failed item.
    /// </summary>
    /// <param name="id">The request identity.</param>
    internal void RecordFailure(Guid id)
    {
        Update(id, status => new BackgroundReconciliationStatus(
            status.Id,
            status.ConfigurationRevision,
            status.State,
            status.TotalItemCount,
            status.CompletedItemCount,
            checked(status.FailedItemCount + 1)));
    }

    /// <summary>
    /// Selects the terminal state from item outcomes.
    /// </summary>
    /// <param name="id">The request identity.</param>
    internal void MarkFinished(Guid id)
    {
        Update(id, status => new BackgroundReconciliationStatus(
            status.Id,
            status.ConfigurationRevision,
            GetTerminalState(status),
            status.TotalItemCount,
            status.CompletedItemCount,
            status.FailedItemCount));
    }

    private Guid Create(long revision, BackgroundReconciliationState state, int totalItemCount)
    {
        var id = Guid.NewGuid();
        lock (_sync)
        {
            _statuses.Add(id, new BackgroundReconciliationStatus(
                id,
                revision,
                state,
                totalItemCount,
                completedItemCount: 0,
                failedItemCount: 0));
        }

        return id;
    }

    private static BackgroundReconciliationState GetTerminalState(BackgroundReconciliationStatus status)
    {
        if (status.FailedItemCount == 0)
        {
            return BackgroundReconciliationState.Completed;
        }

        return status.CompletedItemCount == 0
            ? BackgroundReconciliationState.Failed
            : BackgroundReconciliationState.PartiallyFailed;
    }

    private void Update(
        Guid id,
        Func<BackgroundReconciliationStatus, BackgroundReconciliationStatus> update)
    {
        lock (_sync)
        {
            _statuses[id] = update(_statuses[id]);
        }
    }
}
