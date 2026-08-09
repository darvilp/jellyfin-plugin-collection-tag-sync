using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Reports the complete safety evaluation for one bulk plan.
/// </summary>
public sealed class DestructiveCircuitBreakerResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DestructiveCircuitBreakerResult"/> class.
    /// </summary>
    /// <param name="shouldPause">Whether the entire plan must pause.</param>
    /// <param name="exceedsAbsoluteLimit">Whether the unique affected-item limit is exceeded.</param>
    /// <param name="uniqueAffectedItemCount">The unique affected-item count.</param>
    /// <param name="removals">The exact normalized Authoritative removal tuples.</param>
    /// <param name="groups">The removal-bearing group evaluations.</param>
    internal DestructiveCircuitBreakerResult(
        bool shouldPause,
        bool exceedsAbsoluteLimit,
        int uniqueAffectedItemCount,
        IEnumerable<DestructiveRemoval> removals,
        IEnumerable<DestructiveGroupEvaluation> groups)
    {
        ShouldPause = shouldPause;
        ExceedsAbsoluteLimit = exceedsAbsoluteLimit;
        UniqueAffectedItemCount = uniqueAffectedItemCount;
        Removals = Array.AsReadOnly([.. removals]);
        Groups = Array.AsReadOnly([.. groups]);
    }

    /// <summary>Gets a value indicating whether the entire plan must pause.</summary>
    public bool ShouldPause { get; }

    /// <summary>Gets a value indicating whether the unique affected-item limit is exceeded.</summary>
    public bool ExceedsAbsoluteLimit { get; }

    /// <summary>Gets the unique affected-item count.</summary>
    public int UniqueAffectedItemCount { get; }

    /// <summary>Gets the exact normalized Authoritative removal tuples.</summary>
    public IReadOnlyList<DestructiveRemoval> Removals { get; }

    /// <summary>Gets the removal-bearing group evaluations.</summary>
    public IReadOnlyList<DestructiveGroupEvaluation> Groups { get; }
}
