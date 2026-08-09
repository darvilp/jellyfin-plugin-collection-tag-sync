namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies a stable ordinary configuration activation failure.
/// </summary>
public enum ConfigurationActivationErrorCode
{
    /// <summary>
    /// Domain configuration validation failed.
    /// </summary>
    InvalidCandidate,

    /// <summary>
    /// A newly selected collection GUID does not resolve.
    /// </summary>
    MissingCollection,
}
