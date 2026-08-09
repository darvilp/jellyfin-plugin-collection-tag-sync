namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Determines how a supported target combines with its observed state.
/// </summary>
public enum MappingPolicy
{
    /// <summary>
    /// Preserve observed target state and add supported state.
    /// </summary>
    Additive,

    /// <summary>
    /// Make configured sources authoritative for target state.
    /// </summary>
    Authoritative,
}
