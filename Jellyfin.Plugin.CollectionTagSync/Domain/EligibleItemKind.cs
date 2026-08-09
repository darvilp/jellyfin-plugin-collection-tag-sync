namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Identifies the Jellyfin-independent V1 item shape being planned.
/// </summary>
public enum EligibleItemKind
{
    /// <summary>
    /// A Movie item.
    /// </summary>
    Movie,

    /// <summary>
    /// A Series item.
    /// </summary>
    Series,
}
