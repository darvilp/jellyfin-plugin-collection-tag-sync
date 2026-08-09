namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of confirming one run-once request.
/// </summary>
public enum RunOnceExecutionOutcome
{
    /// <summary>The exact recomputed plan was queued for background execution.</summary>
    Accepted,

    /// <summary>The request failed server-side validation.</summary>
    Invalid,

    /// <summary>The request, revision, exclusions, or removal set requires a new preview.</summary>
    RequiresPreview,

    /// <summary>The authorization is missing, expired, already used, restart-invalidated, or belongs to another administrator.</summary>
    InvalidAuthorization,
}
