using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Identifies one normalized Authoritative removal in a bulk plan.
/// </summary>
public sealed class DestructiveRemoval : IEquatable<DestructiveRemoval>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DestructiveRemoval"/> class.
    /// </summary>
    /// <param name="itemId">The eligible item identifier.</param>
    /// <param name="target">The normalized mapping-group target.</param>
    /// <param name="kind">The direct removal kind.</param>
    public DestructiveRemoval(Guid itemId, Node target, PlannedMutationKind kind)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (kind is not PlannedMutationKind.RemoveTag
            and not PlannedMutationKind.RemoveCollectionMembership)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ItemId = itemId;
        Target = target;
        Kind = kind;
    }

    /// <summary>Gets the eligible item identifier.</summary>
    public Guid ItemId { get; }

    /// <summary>Gets the normalized mapping-group target.</summary>
    public Node Target { get; }

    /// <summary>Gets the direct removal kind.</summary>
    public PlannedMutationKind Kind { get; }

    /// <inheritdoc />
    public bool Equals(DestructiveRemoval? other)
    {
        return other is not null
            && ItemId.Equals(other.ItemId)
            && Target.Equals(other.Target)
            && Kind == other.Kind;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DestructiveRemoval);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ItemId, Target, Kind);
}
