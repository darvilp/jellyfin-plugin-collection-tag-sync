using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides one serializer-stable unresolved-group warning to the administrator UI.
/// </summary>
public sealed class UnresolvedMappingGroupStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnresolvedMappingGroupStatus"/> class.
    /// </summary>
    /// <param name="groupIndex">The persisted group index.</param>
    /// <param name="targetLabel">The administrator-facing target label.</param>
    /// <param name="missingCollections">The unresolved collection identities.</param>
    internal UnresolvedMappingGroupStatus(
        int groupIndex,
        string targetLabel,
        IEnumerable<CollectionPickerEntry> missingCollections)
    {
        GroupIndex = groupIndex;
        TargetLabel = targetLabel;
        MissingCollections = [.. missingCollections];
    }

    /// <summary>Gets the persisted group index.</summary>
    public int GroupIndex { get; }

    /// <summary>Gets the administrator-facing target label.</summary>
    public string TargetLabel { get; }

    /// <summary>Gets the unresolved collection identities.</summary>
    public IReadOnlyList<CollectionPickerEntry> MissingCollections { get; }
}
