namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of validating an ordinary configuration activation request.
/// </summary>
public enum ConfigurationActivationOutcome
{
    /// <summary>
    /// The candidate was persisted and background reconciliation was queued.
    /// </summary>
    Accepted,

    /// <summary>
    /// The candidate failed structural, graph, identity, or reference validation.
    /// </summary>
    Invalid,

    /// <summary>
    /// The candidate would remove metadata and requires the later preview workflow.
    /// </summary>
    RequiresPreview,

    /// <summary>
    /// The supplied preview authorization was missing, expired, consumed, or owned by another administrator.
    /// </summary>
    InvalidAuthorization,
}
