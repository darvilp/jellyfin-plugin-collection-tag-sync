using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Produces settled direct-state mutations for one eligible item.
/// </summary>
public static class ReconciliationPlanner
{
    /// <summary>
    /// Plans one item against a validated mapping configuration.
    /// </summary>
    /// <param name="configuration">The validated mapping configuration.</param>
    /// <param name="observedState">The immutable direct-state snapshot.</param>
    /// <returns>The settled target evaluations and required mutations.</returns>
    public static ReconciliationPlan Plan(
        MappingConfiguration configuration,
        ObservedItemState observedState)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(observedState);

        var effectiveStates = new Dictionary<Node, bool>();
        var evaluations = new List<TargetEvaluation>();
        var mutations = new List<PlannedMutation>();
        var enabledGroups = configuration.Groups
            .Where(group => group.IsEnabled)
            .ToDictionary(group => group.Target);

        foreach (var node in configuration.ActiveGraph.TopologicalOrder)
        {
            var observed = observedState.Contains(node);
            if (!enabledGroups.TryGetValue(node, out var group))
            {
                effectiveStates[node] = observed;
                continue;
            }

            var supportingSources = group.Sources
                .Where(source => effectiveStates[source])
                .OrderBy(source => source, NodeComparer.Instance)
                .ToArray();
            var effective = ReconciliationPlanningSemantics.GetEffectiveState(
                group.Policy,
                observed,
                supportingSources);
            effectiveStates[node] = effective;
            evaluations.Add(new TargetEvaluation(
                node,
                group.Policy,
                observed,
                effective,
                supportingSources));

            if (observed == effective)
            {
                continue;
            }

            mutations.Add(ReconciliationPlanningSemantics.CreateMutation(
                node,
                group.Policy,
                effective,
                supportingSources,
                observedState));
        }

        return new ReconciliationPlan(
            observedState.ItemId,
            observedState.ItemKind,
            evaluations,
            mutations);
    }
}
