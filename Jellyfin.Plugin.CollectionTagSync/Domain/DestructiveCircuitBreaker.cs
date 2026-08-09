using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Evaluates complete bulk plans against the accepted destructive safety limits.
/// </summary>
public static class DestructiveCircuitBreaker
{
    /// <summary>
    /// Evaluates one complete bulk plan without applying any mutation.
    /// </summary>
    /// <param name="plans">Every successfully calculated item plan in the bulk run.</param>
    /// <param name="options">The accepted safety limits.</param>
    /// <returns>The deterministic safety evaluation and exact removal set.</returns>
    public static DestructiveCircuitBreakerResult Evaluate(
        IEnumerable<ReconciliationPlan> plans,
        DestructiveCircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(options);

        var planArray = plans.ToArray();
        var removals = DestructiveRemovalSet.FromPlans(planArray);
        var uniqueAffectedItemCount = removals
            .Select(removal => removal.ItemId)
            .Distinct()
            .Count();
        var exceedsAbsoluteLimit = uniqueAffectedItemCount > options.MaximumAffectedItems;

        var groups = removals
            .Select(removal => removal.Target)
            .Distinct()
            .OrderBy(target => target, NodeComparer.Instance)
            .Select(target => EvaluateGroup(planArray, removals, target, options))
            .ToArray();
        var exceedsAnyLimit = exceedsAbsoluteLimit || groups.Any(group => group.ExceedsPercentageLimit);
        return new DestructiveCircuitBreakerResult(
            options.IsEnabled && exceedsAnyLimit,
            exceedsAbsoluteLimit,
            uniqueAffectedItemCount,
            removals,
            groups);
    }

    private static DestructiveGroupEvaluation EvaluateGroup(
        IEnumerable<ReconciliationPlan> plans,
        IEnumerable<DestructiveRemoval> removals,
        Node target,
        DestructiveCircuitBreakerOptions options)
    {
        var currentAssignmentCount = plans.Count(plan => plan.TargetEvaluations.Any(evaluation =>
            evaluation.Policy == MappingPolicy.Authoritative
            && evaluation.Target.Equals(target)
            && evaluation.ObservedState));
        var removalCount = removals.Count(removal => removal.Target.Equals(target));
        var exceedsPercentageLimit = currentAssignmentCount >= options.MinimumAssignmentPopulation
            && (long)removalCount * 100
                > (long)currentAssignmentCount * options.MaximumRemovalPercentage;
        return new DestructiveGroupEvaluation(
            target,
            currentAssignmentCount,
            removalCount,
            exceedsPercentageLimit);
    }
}
