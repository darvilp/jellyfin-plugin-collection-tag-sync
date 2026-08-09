namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Identifies a stable candidate configuration validation failure.
/// </summary>
public enum MappingValidationErrorCode
{
    /// <summary>
    /// A configured tag is empty after trimming.
    /// </summary>
    EmptyTag,

    /// <summary>
    /// A mapping group has no sources.
    /// </summary>
    NoSources,

    /// <summary>
    /// A mapping group contains its own target as a source.
    /// </summary>
    SelfSource,

    /// <summary>
    /// A mapping group contains the same normalized source more than once.
    /// </summary>
    DuplicateSource,

    /// <summary>
    /// More than one persisted mapping group has the same normalized target.
    /// </summary>
    DuplicateTarget,

    /// <summary>
    /// A configured collection has no Jellyfin identity.
    /// </summary>
    InvalidCollectionId,

    /// <summary>
    /// A mapping group specifies an unsupported target policy.
    /// </summary>
    InvalidPolicy,
}
