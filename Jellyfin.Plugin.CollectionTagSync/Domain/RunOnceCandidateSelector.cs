using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Selects every eligible item whose run-once final plan can require work.
/// </summary>
public static class RunOnceCandidateSelector
{
    /// <summary>
    /// Selects candidates while preserving whole-library planning semantics.
    /// </summary>
    /// <param name="activeConfiguration">The validated persisted configuration.</param>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="observedStates">All eligible Movie and Series snapshots.</param>
    /// <param name="excludedItemIds">Items retaining their observed direct target state.</param>
    /// <returns>The item identifiers whose final plans require evaluation or retain an exclusion.</returns>
    public static IReadOnlySet<Guid> Select(
        MappingConfiguration activeConfiguration,
        RunOnceOperation operation,
        IEnumerable<ObservedItemState> observedStates,
        IEnumerable<Guid> excludedItemIds)
    {
        return SelectPlans(
            activeConfiguration,
            operation,
            observedStates,
            excludedItemIds)
            .Select(plan => plan.ItemId)
            .ToHashSet();
    }

    /// <summary>
    /// Selects and plans candidates once while preserving whole-library semantics.
    /// </summary>
    /// <param name="activeConfiguration">The validated persisted configuration.</param>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="observedStates">All eligible Movie and Series snapshots.</param>
    /// <param name="excludedItemIds">Items retaining their observed direct target state.</param>
    /// <returns>The final plans that require execution or retain an exclusion.</returns>
    public static IReadOnlyList<ReconciliationPlan> SelectPlans(
        MappingConfiguration activeConfiguration,
        RunOnceOperation operation,
        IEnumerable<ObservedItemState> observedStates,
        IEnumerable<Guid> excludedItemIds)
    {
        ArgumentNullException.ThrowIfNull(activeConfiguration);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(observedStates);
        ArgumentNullException.ThrowIfNull(excludedItemIds);

        return SelectPlanDetails(
            activeConfiguration,
            operation,
            observedStates,
            excludedItemIds).Plans;
    }

    /// <summary>
    /// Plans candidates once and identifies direct operation-target exclusion choices.
    /// </summary>
    /// <param name="activeConfiguration">The validated persisted configuration.</param>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="observedStates">All eligible Movie and Series snapshots.</param>
    /// <param name="excludedItemIds">Items retaining their observed direct target state.</param>
    /// <returns>The selected plans and direct-target exclusion choices.</returns>
    public static RunOnceCandidateSelection SelectPlanDetails(
        MappingConfiguration activeConfiguration,
        RunOnceOperation operation,
        IEnumerable<ObservedItemState> observedStates,
        IEnumerable<Guid> excludedItemIds)
    {
        ArgumentNullException.ThrowIfNull(activeConfiguration);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(observedStates);
        ArgumentNullException.ThrowIfNull(excludedItemIds);

        var exclusions = new HashSet<Guid>(excludedItemIds);
        var plans = new List<ReconciliationPlan>();
        var directTargetChangeItemIds = new HashSet<Guid>();
        foreach (var state in observedStates)
        {
            var isExcluded = exclusions.Contains(state.ItemId);
            var directPlan = RunOncePlanner.Plan(
                activeConfiguration,
                operation,
                state,
                keepCurrentTargetState: false);
            if (directPlan.Mutations.Any(mutation => mutation.Target.Equals(operation.Target)))
            {
                directTargetChangeItemIds.Add(state.ItemId);
            }

            var plan = isExcluded
                ? RunOncePlanner.Plan(
                    activeConfiguration,
                    operation,
                    state,
                    keepCurrentTargetState: true)
                : directPlan;
            if (isExcluded || plan.Mutations.Count > 0)
            {
                plans.Add(plan);
            }
        }

        return new RunOnceCandidateSelection(plans, directTargetChangeItemIds);
    }
}
