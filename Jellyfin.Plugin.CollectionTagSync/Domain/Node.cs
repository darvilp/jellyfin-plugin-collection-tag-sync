using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents a validated synchronization node.
/// </summary>
public abstract class Node : IEquatable<Node>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Node"/> class.
    /// </summary>
    private protected Node()
    {
    }

    /// <inheritdoc />
    public abstract bool Equals(Node? other);

    /// <inheritdoc />
    public sealed override bool Equals(object? obj)
    {
        return Equals(obj as Node);
    }

    /// <inheritdoc />
    public abstract override int GetHashCode();
}
