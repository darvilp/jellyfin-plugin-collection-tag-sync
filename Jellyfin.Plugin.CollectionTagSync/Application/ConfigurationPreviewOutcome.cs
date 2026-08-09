namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of planning one complete candidate configuration.
/// </summary>
public enum ConfigurationPreviewOutcome
{
    /// <summary>The candidate is valid and the complete preview is available.</summary>
    Ready,

    /// <summary>The candidate failed server-side validation.</summary>
    Invalid,
}
