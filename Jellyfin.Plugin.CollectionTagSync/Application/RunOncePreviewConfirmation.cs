using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries the exact request identity and destructive plan authorized by one run-once preview.
/// </summary>
internal sealed class RunOncePreviewConfirmation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOncePreviewConfirmation"/> class.
    /// </summary>
    /// <param name="groupId">The selected saved-group identity, or empty for an internal transient request.</param>
    /// <param name="operationFingerprint">The canonical operation and exclusion identity.</param>
    /// <param name="activeRevision">The active revision used during preview.</param>
    /// <param name="expectedRemovals">The exact authorized removal tuples.</param>
    public RunOncePreviewConfirmation(
        Guid groupId,
        string operationFingerprint,
        long activeRevision,
        IEnumerable<DestructiveRemoval> expectedRemovals)
    {
        GroupId = groupId;
        OperationFingerprint = operationFingerprint;
        ActiveRevision = activeRevision;
        ExpectedRemovals = [.. expectedRemovals];
    }

    /// <summary>Gets the selected saved-group identity.</summary>
    public Guid GroupId { get; }

    /// <summary>Gets the canonical operation and exclusion identity.</summary>
    public string OperationFingerprint { get; }

    /// <summary>Gets the active revision used during preview.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the exact authorized removal tuples.</summary>
    public IReadOnlyList<DestructiveRemoval> ExpectedRemovals { get; }
}
