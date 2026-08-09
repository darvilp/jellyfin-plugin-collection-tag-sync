namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Identifies a direct metadata mutation required by a settled plan.
/// </summary>
public enum PlannedMutationKind
{
    /// <summary>
    /// Add a direct tag.
    /// </summary>
    AddTag,

    /// <summary>
    /// Remove one logical direct tag identity.
    /// </summary>
    RemoveTag,

    /// <summary>
    /// Add direct collection membership.
    /// </summary>
    AddCollectionMembership,

    /// <summary>
    /// Remove direct collection membership.
    /// </summary>
    RemoveCollectionMembership,
}
