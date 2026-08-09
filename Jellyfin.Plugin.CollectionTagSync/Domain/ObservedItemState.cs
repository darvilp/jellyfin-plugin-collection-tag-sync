using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents one immutable snapshot of direct eligible-item state.
/// </summary>
public sealed class ObservedItemState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedItemState"/> class.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="itemKind">The eligible item kind.</param>
    /// <param name="directTags">The exact directly stored tags.</param>
    /// <param name="directCollectionIds">The direct collection memberships.</param>
    public ObservedItemState(
        Guid itemId,
        EligibleItemKind itemKind,
        IEnumerable<string> directTags,
        IEnumerable<Guid> directCollectionIds)
    {
        ArgumentNullException.ThrowIfNull(directTags);
        ArgumentNullException.ThrowIfNull(directCollectionIds);

        ItemId = itemId;
        ItemKind = itemKind;
        DirectTags = Array.AsReadOnly([.. directTags]);
        DirectCollectionIds = Array.AsReadOnly([.. directCollectionIds.Distinct()]);
    }

    /// <summary>
    /// Gets the item identifier.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the eligible item kind.
    /// </summary>
    public EligibleItemKind ItemKind { get; }

    /// <summary>
    /// Gets the exact directly stored tags.
    /// </summary>
    public IReadOnlyList<string> DirectTags { get; }

    /// <summary>
    /// Gets the direct collection memberships.
    /// </summary>
    public IReadOnlyList<Guid> DirectCollectionIds { get; }

    /// <summary>
    /// Determines whether the snapshot directly contains a node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when the node is directly present.</returns>
    internal bool Contains(Node node)
    {
        return node switch
        {
            TagNode tag => DirectTags.Any(value =>
                StringComparer.OrdinalIgnoreCase.Equals(value.Trim(), tag.Value)),
            CollectionNode collection => DirectCollectionIds.Contains(collection.Id),
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }

    /// <summary>
    /// Gets every exact stored spelling matching one normalized tag identity.
    /// </summary>
    /// <param name="tag">The normalized tag node.</param>
    /// <returns>The deterministic exact stored spellings.</returns>
    internal IEnumerable<string> GetMatchingTagValues(TagNode tag)
    {
        return DirectTags
            .Where(value => StringComparer.OrdinalIgnoreCase.Equals(value.Trim(), tag.Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }
}
