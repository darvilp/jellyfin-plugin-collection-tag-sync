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
            var supported = supportingSources.Length > 0;
            var effective = group.Policy == MappingPolicy.Additive
                ? observed || supported
                : supported;
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

            mutations.Add(new PlannedMutation(
                GetMutationKind(node, effective),
                node,
                group.Policy,
                supportingSources,
                GetTagValues(node, effective, observedState)));
        }

        return new ReconciliationPlan(
            observedState.ItemId,
            observedState.ItemKind,
            evaluations,
            mutations);
    }

    private static PlannedMutationKind GetMutationKind(Node node, bool effectiveState)
    {
        return (node, effectiveState) switch
        {
            (TagNode, true) => PlannedMutationKind.AddTag,
            (TagNode, false) => PlannedMutationKind.RemoveTag,
            (CollectionNode, true) => PlannedMutationKind.AddCollectionMembership,
            (CollectionNode, false) => PlannedMutationKind.RemoveCollectionMembership,
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }

    private static IEnumerable<string> GetTagValues(
        Node node,
        bool effectiveState,
        ObservedItemState observedState)
    {
        if (node is not TagNode tag)
        {
            return [];
        }

        return effectiveState
            ? [tag.Value]
            : observedState.GetMatchingTagValues(tag);
    }
}
