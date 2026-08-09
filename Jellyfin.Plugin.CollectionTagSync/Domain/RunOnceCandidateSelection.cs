using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Contains selected run-once plans and server-authoritative direct-target exclusion choices.
/// </summary>
public sealed class RunOnceCandidateSelection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceCandidateSelection"/> class.
    /// </summary>
    /// <param name="plans">The final selected plans.</param>
    /// <param name="directTargetChangeItemIds">Items with a direct operation-target change.</param>
    internal RunOnceCandidateSelection(
        IEnumerable<ReconciliationPlan> plans,
        IEnumerable<Guid> directTargetChangeItemIds)
    {
        Plans = [.. plans];
        DirectTargetChangeItemIds = new HashSet<Guid>(directTargetChangeItemIds);
    }

    /// <summary>Gets the final plans that require execution or retain an exclusion.</summary>
    public IReadOnlyList<ReconciliationPlan> Plans { get; }

    /// <summary>Gets items whose direct operation-target change may be excluded.</summary>
    public IReadOnlySet<Guid> DirectTargetChangeItemIds { get; }
}
