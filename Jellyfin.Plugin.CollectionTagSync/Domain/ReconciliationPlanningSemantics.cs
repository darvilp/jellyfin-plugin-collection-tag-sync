using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Shares one target's settled-state and mutation semantics across planning workflows.
/// </summary>
internal static class ReconciliationPlanningSemantics
{
    /// <summary>Evaluates one target using the shared Additive or Authoritative rule.</summary>
    /// <param name="policy">The mapping policy.</param>
    /// <param name="observedState">The target's observed direct state.</param>
    /// <param name="supportingSources">The effective supporting sources.</param>
    /// <returns>The target's final effective state.</returns>
    public static bool GetEffectiveState(
        MappingPolicy policy,
        bool observedState,
        IReadOnlyCollection<Node> supportingSources)
    {
        return policy == MappingPolicy.Additive
            ? observedState || supportingSources.Count > 0
            : supportingSources.Count > 0;
    }

    /// <summary>Creates the direct mutation required to reach one effective target state.</summary>
    /// <param name="target">The target node.</param>
    /// <param name="policy">The mapping policy.</param>
    /// <param name="effectiveState">The final effective state.</param>
    /// <param name="supportingSources">The effective supporting sources.</param>
    /// <param name="observedState">The immutable direct-state snapshot.</param>
    /// <returns>The required direct mutation.</returns>
    public static PlannedMutation CreateMutation(
        Node target,
        MappingPolicy policy,
        bool effectiveState,
        IReadOnlyCollection<Node> supportingSources,
        ObservedItemState observedState)
    {
        return new PlannedMutation(
            GetMutationKind(target, effectiveState),
            target,
            policy,
            supportingSources,
            GetTagValues(target, effectiveState, observedState));
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
