using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Orders normalized node identities deterministically.
/// </summary>
internal sealed class NodeComparer : IComparer<Node>
{
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static NodeComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(Node? x, Node? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return (x, y) switch
        {
            (TagNode left, TagNode right) => StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value),
            (CollectionNode left, CollectionNode right) => left.Id.CompareTo(right.Id),
            (TagNode, CollectionNode) => -1,
            (CollectionNode, TagNode) => 1,
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }
}
