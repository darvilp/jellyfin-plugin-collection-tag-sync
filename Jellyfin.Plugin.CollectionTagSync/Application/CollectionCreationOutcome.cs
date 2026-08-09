namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of one independent collection-creation request.
/// </summary>
public enum CollectionCreationOutcome
{
    /// <summary>A new collection was created and returned as the selected value.</summary>
    Created,

    /// <summary>The requested name was empty after trimming.</summary>
    InvalidName,

    /// <summary>One or more existing collection names matched after normalization.</summary>
    DuplicateName,
}
