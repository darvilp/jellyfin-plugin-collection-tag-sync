using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Extracts deterministic normalized Authoritative removal tuples from settled plans.
/// </summary>
internal static class DestructiveRemovalSet
{
    /// <summary>Extracts the exact removal set.</summary>
    /// <param name="plans">Every successfully calculated item plan.</param>
    /// <returns>The exact deterministic removal tuples.</returns>
    public static IReadOnlyList<DestructiveRemoval> FromPlans(
        IEnumerable<ReconciliationPlan> plans)
    {
        return plans
            .SelectMany(plan => plan.Mutations
                .Where(IsAuthoritativeRemoval)
                .Select(mutation => new DestructiveRemoval(plan.ItemId, mutation.Target, mutation.Kind)))
            .Distinct()
            .OrderBy(removal => removal.ItemId)
            .ThenBy(removal => removal.Target, NodeComparer.Instance)
            .ThenBy(removal => removal.Kind)
            .ToArray();
    }

    private static bool IsAuthoritativeRemoval(PlannedMutation mutation)
    {
        return mutation.Policy == MappingPolicy.Authoritative
            && mutation.Kind is PlannedMutationKind.RemoveTag
                or PlannedMutationKind.RemoveCollectionMembership;
    }
}
