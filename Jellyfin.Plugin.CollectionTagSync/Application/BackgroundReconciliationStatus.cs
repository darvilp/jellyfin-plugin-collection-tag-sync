using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides privacy-safe progress for one background reconciliation request.
/// </summary>
public sealed class BackgroundReconciliationStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundReconciliationStatus"/> class.
    /// </summary>
    /// <param name="id">The opaque request identity.</param>
    /// <param name="configurationRevision">The configuration revision.</param>
    /// <param name="state">The lifecycle state.</param>
    /// <param name="totalItemCount">The total eligible item count.</param>
    /// <param name="completedItemCount">The successful item count.</param>
    /// <param name="failedItemCount">The failed item count.</param>
    internal BackgroundReconciliationStatus(
        Guid id,
        long configurationRevision,
        BackgroundReconciliationState state,
        int totalItemCount,
        int completedItemCount,
        int failedItemCount)
    {
        Id = id;
        ConfigurationRevision = configurationRevision;
        State = state;
        TotalItemCount = totalItemCount;
        CompletedItemCount = completedItemCount;
        FailedItemCount = failedItemCount;
    }

    /// <summary>
    /// Gets the opaque reconciliation identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the accepted configuration revision, or the still-active revision for a paused request.
    /// </summary>
    public long ConfigurationRevision { get; }

    /// <summary>
    /// Gets the lifecycle state.
    /// </summary>
    public BackgroundReconciliationState State { get; }

    /// <summary>
    /// Gets the eligible item count without exposing identities.
    /// </summary>
    public int TotalItemCount { get; }

    /// <summary>
    /// Gets the successfully completed item count.
    /// </summary>
    public int CompletedItemCount { get; }

    /// <summary>
    /// Gets the failed item count.
    /// </summary>
    public int FailedItemCount { get; }
}
