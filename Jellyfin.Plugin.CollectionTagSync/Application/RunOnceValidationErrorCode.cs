namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies stable run-once request validation failures.
/// </summary>
public enum RunOnceValidationErrorCode
{
    /// <summary>The mapping-shaped operation is invalid.</summary>
    InvalidOperation,

    /// <summary>A selected collection GUID does not resolve.</summary>
    MissingCollection,

    /// <summary>An enabled continuous group already owns the requested target.</summary>
    TargetConflict,

    /// <summary>An exclusion does not identify a currently eligible direct target change.</summary>
    InvalidExclusion,
}
