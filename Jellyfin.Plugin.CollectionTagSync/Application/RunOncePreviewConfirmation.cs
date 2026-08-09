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
    /// <param name="operationFingerprint">The canonical operation and exclusion identity.</param>
    /// <param name="activeRevision">The active revision used during preview.</param>
    /// <param name="expectedRemovals">The exact authorized removal tuples.</param>
    public RunOncePreviewConfirmation(
        string operationFingerprint,
        long activeRevision,
        IEnumerable<DestructiveRemoval> expectedRemovals)
    {
        OperationFingerprint = operationFingerprint;
        ActiveRevision = activeRevision;
        ExpectedRemovals = [.. expectedRemovals];
    }

    /// <summary>Gets the canonical operation and exclusion identity.</summary>
    public string OperationFingerprint { get; }

    /// <summary>Gets the active revision used during preview.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the exact authorized removal tuples.</summary>
    public IReadOnlyList<DestructiveRemoval> ExpectedRemovals { get; }
}
