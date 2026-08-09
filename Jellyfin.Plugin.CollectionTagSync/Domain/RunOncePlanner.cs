using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Produces a staged one-time target plan followed by affected continuous cascades.
/// </summary>
public static class RunOncePlanner
{
    /// <summary>
    /// Plans one eligible item without adding the run-once operation to the active graph.
    /// </summary>
    /// <param name="activeConfiguration">The validated persisted configuration.</param>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="observedState">The immutable direct-state snapshot.</param>
    /// <param name="keepCurrentTargetState">Whether to retain the observed direct run-once target state.</param>
    /// <returns>The direct operation and affected downstream settled-state plan.</returns>
    public static ReconciliationPlan Plan(
        MappingConfiguration activeConfiguration,
        RunOnceOperation operation,
        ObservedItemState observedState,
        bool keepCurrentTargetState)
    {
        ArgumentNullException.ThrowIfNull(activeConfiguration);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(observedState);

        var activeGroups = activeConfiguration.Groups
            .Where(group => group.IsEnabled)
            .ToDictionary(group => group.Target);
        var effectiveStates = EstablishBaseline(
            activeConfiguration,
            observedState,
            activeGroups);
        var evaluations = new List<TargetEvaluation>();
        var mutations = new List<PlannedMutation>();

        EvaluateRunOnceTarget(
            operation,
            observedState,
            effectiveStates,
            keepCurrentTargetState,
            evaluations,
            mutations);
        EvaluateDownstreamTargets(
            activeConfiguration,
            observedState,
            activeGroups,
            effectiveStates,
            operation.Target,
            evaluations,
            mutations);

        return new ReconciliationPlan(
            observedState.ItemId,
            observedState.ItemKind,
            evaluations,
            mutations);
    }

    private static Dictionary<Node, bool> EstablishBaseline(
        MappingConfiguration activeConfiguration,
        ObservedItemState observedState,
        Dictionary<Node, MappingGroup> activeGroups)
    {
        var effectiveStates = new Dictionary<Node, bool>();
        foreach (var node in activeConfiguration.ActiveGraph.TopologicalOrder)
        {
            if (!activeGroups.TryGetValue(node, out var group))
            {
                effectiveStates[node] = observedState.Contains(node);
                continue;
            }

            var supportingSources = GetSupportingSources(group.Sources, effectiveStates, observedState);
            effectiveStates[node] = ReconciliationPlanningSemantics.GetEffectiveState(
                group.Policy,
                observedState.Contains(node),
                supportingSources);
        }

        return effectiveStates;
    }

    private static void EvaluateRunOnceTarget(
        RunOnceOperation operation,
        ObservedItemState observedState,
        Dictionary<Node, bool> effectiveStates,
        bool keepCurrentTargetState,
        List<TargetEvaluation> evaluations,
        List<PlannedMutation> mutations)
    {
        var observed = observedState.Contains(operation.Target);
        var supportingSources = GetSupportingSources(operation.Sources, effectiveStates, observedState);
        var effective = keepCurrentTargetState
            ? observed
            : ReconciliationPlanningSemantics.GetEffectiveState(
                operation.Policy,
                observed,
                supportingSources);
        effectiveStates[operation.Target] = effective;
        evaluations.Add(new TargetEvaluation(
            operation.Target,
            operation.Policy,
            observed,
            effective,
            supportingSources));

        if (observed != effective)
        {
            mutations.Add(ReconciliationPlanningSemantics.CreateMutation(
                operation.Target,
                operation.Policy,
                effective,
                supportingSources,
                observedState));
        }
    }

    private static void EvaluateDownstreamTargets(
        MappingConfiguration activeConfiguration,
        ObservedItemState observedState,
        Dictionary<Node, MappingGroup> activeGroups,
        Dictionary<Node, bool> effectiveStates,
        Node runOnceTarget,
        List<TargetEvaluation> evaluations,
        List<PlannedMutation> mutations)
    {
        var affectedNodes = new HashSet<Node> { runOnceTarget };
        foreach (var node in activeConfiguration.ActiveGraph.TopologicalOrder)
        {
            if (!activeGroups.TryGetValue(node, out var group)
                || node.Equals(runOnceTarget)
                || !group.Sources.Any(affectedNodes.Contains))
            {
                continue;
            }

            affectedNodes.Add(node);
            var observed = observedState.Contains(node);
            var supportingSources = GetSupportingSources(group.Sources, effectiveStates, observedState);
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

            if (observed != effective)
            {
                mutations.Add(ReconciliationPlanningSemantics.CreateMutation(
                    node,
                    group.Policy,
                    effective,
                    supportingSources,
                    observedState));
            }
        }
    }

    private static Node[] GetSupportingSources(
        IEnumerable<Node> sources,
        Dictionary<Node, bool> effectiveStates,
        ObservedItemState observedState)
    {
        return sources
            .Where(source => effectiveStates.TryGetValue(source, out var effective)
                ? effective
                : observedState.Contains(source))
            .OrderBy(source => source, NodeComparer.Instance)
            .ToArray();
    }
}
