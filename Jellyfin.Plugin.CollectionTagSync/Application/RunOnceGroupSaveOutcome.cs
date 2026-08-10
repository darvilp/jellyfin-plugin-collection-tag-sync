namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies the result of validating and persisting one reusable run-once group.
/// </summary>
public enum RunOnceGroupSaveOutcome
{
    /// <summary>The group was validated and persisted.</summary>
    Saved,

    /// <summary>The group was rejected by server validation.</summary>
    Invalid,
}
