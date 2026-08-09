namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the server-authoritative outcome of one paused-plan confirmation.
/// </summary>
public enum FullReconcileConfirmationOutcome
{
    /// <summary>The fresh equivalent plan was accepted for execution.</summary>
    Accepted,

    /// <summary>The authorization was absent, invalid, expired, reused, or belonged to another administrator.</summary>
    InvalidAuthorization,

    /// <summary>The fresh plan no longer had the exact authorized removal set.</summary>
    StalePreview,
}
