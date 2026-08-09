using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries the exact candidate identity and destructive plan authorized by one preview.
/// </summary>
internal sealed class ConfigurationPreviewConfirmation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationPreviewConfirmation"/> class.
    /// </summary>
    /// <param name="candidateFingerprint">The canonical complete candidate identity.</param>
    /// <param name="activeRevision">The active revision used during preview.</param>
    /// <param name="expectedRemovals">The exact authorized removal tuples.</param>
    public ConfigurationPreviewConfirmation(
        string candidateFingerprint,
        long activeRevision,
        IEnumerable<DestructiveRemoval> expectedRemovals)
    {
        CandidateFingerprint = candidateFingerprint;
        ActiveRevision = activeRevision;
        ExpectedRemovals = new HashSet<DestructiveRemoval>(expectedRemovals);
    }

    /// <summary>Gets the canonical complete candidate identity.</summary>
    public string CandidateFingerprint { get; }

    /// <summary>Gets the active revision used during preview.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the exact authorized removal tuples.</summary>
    public IReadOnlySet<DestructiveRemoval> ExpectedRemovals { get; }
}
