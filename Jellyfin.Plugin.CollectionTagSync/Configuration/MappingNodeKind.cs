namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Identifies the serializer-friendly kind of a configured mapping node.
/// </summary>
public enum MappingNodeKind
{
    /// <summary>
    /// A direct Jellyfin tag.
    /// </summary>
    Tag,

    /// <summary>
    /// A direct Jellyfin collection membership identified by collection GUID.
    /// </summary>
    Collection,
}
