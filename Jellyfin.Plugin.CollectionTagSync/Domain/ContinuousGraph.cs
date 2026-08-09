using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents the active continuous mapping graph.
/// </summary>
public sealed class ContinuousGraph
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuousGraph"/> class.
    /// </summary>
    /// <param name="edges">The active edges.</param>
    internal ContinuousGraph(IEnumerable<MappingEdge> edges)
    {
        Edges = Array.AsReadOnly([.. edges]);
        CyclePath = Array.AsReadOnly([.. FindCycle(Edges)]);
        TopologicalOrder = Array.AsReadOnly([.. BuildTopologicalOrder(Edges)]);
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }

    /// <summary>
    /// Gets the active source-to-target edges.
    /// </summary>
    public IReadOnlyList<MappingEdge> Edges { get; }

    /// <summary>
    /// Gets a deterministic source-before-target ordering of active nodes.
    /// </summary>
    public IReadOnlyList<Node> TopologicalOrder { get; }

    /// <summary>
    /// Gets the detected complete cycle path, if any.
    /// </summary>
    internal IReadOnlyList<Node> CyclePath { get; }

    private static List<Node> BuildTopologicalOrder(IEnumerable<MappingEdge> edges)
    {
        var nodes = new HashSet<Node>();
        var outgoing = new Dictionary<Node, List<Node>>();
        var inboundCounts = new Dictionary<Node, int>();

        foreach (var edge in edges)
        {
            nodes.Add(edge.Source);
            nodes.Add(edge.Target);
            if (!outgoing.TryGetValue(edge.Source, out var targets))
            {
                targets = [];
                outgoing.Add(edge.Source, targets);
            }

            targets.Add(edge.Target);
            inboundCounts.TryGetValue(edge.Target, out var inboundCount);
            inboundCounts[edge.Target] = inboundCount + 1;
            inboundCounts.TryAdd(edge.Source, 0);
        }

        var ready = new SortedSet<Node>(
            nodes.Where(node => inboundCounts[node] == 0),
            NodeComparer.Instance);
        var order = new List<Node>(nodes.Count);
        while (ready.Count > 0)
        {
            var node = ready.Min!;
            ready.Remove(node);
            order.Add(node);

            if (!outgoing.TryGetValue(node, out var targets))
            {
                continue;
            }

            targets.Sort(NodeComparer.Instance);
            foreach (var target in targets)
            {
                inboundCounts[target]--;
                if (inboundCounts[target] == 0)
                {
                    ready.Add(target);
                }
            }
        }

        return order;
    }

    private static List<Node> FindCycle(IReadOnlyList<MappingEdge> edges)
    {
        var outgoing = new Dictionary<Node, List<Node>>();
        var nodes = new HashSet<Node>();
        foreach (var edge in edges)
        {
            nodes.Add(edge.Source);
            nodes.Add(edge.Target);
            if (!outgoing.TryGetValue(edge.Source, out var targets))
            {
                targets = [];
                outgoing.Add(edge.Source, targets);
            }

            targets.Add(edge.Target);
        }

        foreach (var targets in outgoing.Values)
        {
            targets.Sort(NodeComparer.Instance);
        }

        var states = new Dictionary<Node, VisitState>();
        var stack = new List<Node>();
        foreach (var node in nodes.OrderBy(node => node, NodeComparer.Instance))
        {
            if (states.ContainsKey(node))
            {
                continue;
            }

            var cycle = FindCycleFrom(node, outgoing, states, stack);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return [];
    }

    private static List<Node>? FindCycleFrom(
        Node node,
        IReadOnlyDictionary<Node, List<Node>> outgoing,
        IDictionary<Node, VisitState> states,
        List<Node> stack)
    {
        states[node] = VisitState.Visiting;
        stack.Add(node);

        if (outgoing.TryGetValue(node, out var targets))
        {
            foreach (var target in targets)
            {
                if (!states.TryGetValue(target, out var state))
                {
                    var nestedCycle = FindCycleFrom(target, outgoing, states, stack);
                    if (nestedCycle is not null)
                    {
                        return nestedCycle;
                    }
                }
                else if (state == VisitState.Visiting)
                {
                    var cycleStart = stack.FindIndex(item => item.Equals(target));
                    var cycle = stack.GetRange(cycleStart, stack.Count - cycleStart);
                    cycle.Add(target);
                    return cycle;
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        states[node] = VisitState.Visited;
        return null;
    }
}
