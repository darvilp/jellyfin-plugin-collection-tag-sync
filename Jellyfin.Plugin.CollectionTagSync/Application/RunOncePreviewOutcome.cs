namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of planning one run-once request.
/// </summary>
public enum RunOncePreviewOutcome
{
    /// <summary>The request is valid and its complete preview is available.</summary>
    Ready,

    /// <summary>The request failed server-side validation.</summary>
    Invalid,
}
