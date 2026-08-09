using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries one consumed in-process authorization into a fresh Full Reconcile request.
/// </summary>
internal sealed class FullReconcileConfirmation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileConfirmation"/> class.
    /// </summary>
    /// <param name="pausedRunId">The paused run whose preview was authorized.</param>
    /// <param name="configurationRevision">The active revision bound to the preview.</param>
    /// <param name="expectedRemovals">The exact authorized removal tuples.</param>
    public FullReconcileConfirmation(
        Guid pausedRunId,
        long configurationRevision,
        IEnumerable<DestructiveRemoval> expectedRemovals)
    {
        PausedRunId = pausedRunId;
        ConfigurationRevision = configurationRevision;
        ExpectedRemovals = Array.AsReadOnly([.. expectedRemovals]);
    }

    /// <summary>Gets the paused run whose preview was authorized.</summary>
    public Guid PausedRunId { get; }

    /// <summary>Gets the active revision bound to the preview.</summary>
    public long ConfigurationRevision { get; }

    /// <summary>Gets the exact authorized removal tuples.</summary>
    public IReadOnlyList<DestructiveRemoval> ExpectedRemovals { get; }
}
