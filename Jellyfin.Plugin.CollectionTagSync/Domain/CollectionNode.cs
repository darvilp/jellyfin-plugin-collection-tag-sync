using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents a validated collection identity.
/// </summary>
public sealed class CollectionNode : Node
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionNode"/> class.
    /// </summary>
    /// <param name="id">The Jellyfin collection identifier.</param>
    /// <param name="displayName">The current display name, if known.</param>
    internal CollectionNode(Guid id, string? displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the Jellyfin collection identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the current display name, if known.
    /// </summary>
    public string? DisplayName { get; }

    /// <inheritdoc />
    public override bool Equals(Node? other)
    {
        return other is CollectionNode collection && Id.Equals(collection.Id);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
