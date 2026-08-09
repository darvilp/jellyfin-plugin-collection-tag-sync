using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes one enabled group skipped because collection references are missing.
/// </summary>
public sealed class UnresolvedMappingGroupDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnresolvedMappingGroupDiagnostic"/> class.
    /// </summary>
    /// <param name="groupIndex">The persisted group index.</param>
    /// <param name="target">The group target.</param>
    /// <param name="missingCollections">The missing collection identities.</param>
    internal UnresolvedMappingGroupDiagnostic(
        int groupIndex,
        Node target,
        IEnumerable<CollectionNode> missingCollections)
    {
        GroupIndex = groupIndex;
        Target = target;
        MissingCollections = Array.AsReadOnly([.. missingCollections]);
    }

    /// <summary>
    /// Gets the persisted group index.
    /// </summary>
    public int GroupIndex { get; }

    /// <summary>
    /// Gets the configured target.
    /// </summary>
    public Node Target { get; }

    /// <summary>
    /// Gets every missing collection referenced by the group.
    /// </summary>
    public IReadOnlyList<CollectionNode> MissingCollections { get; }
}
