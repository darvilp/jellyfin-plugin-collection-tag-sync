namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Carries one opaque paused-plan preview authorization.
/// </summary>
public sealed class FullReconcileConfirmationRequest
{
    /// <summary>Gets or sets the opaque single-use authorization.</summary>
    public string Authorization { get; set; } = string.Empty;
}
