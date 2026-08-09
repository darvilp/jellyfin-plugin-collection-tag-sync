using Jellyfin.Plugin.CollectionTagSync.Application;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Carries one exact run-once request and its opaque preview authorization.
/// </summary>
public sealed class RunOnceConfirmationRequest
{
    /// <summary>Gets or sets the exact operation and exclusion set.</summary>
    public RunOnceOperationRequest Operation { get; set; } = new();

    /// <summary>Gets or sets the opaque single-use authorization.</summary>
    public string Authorization { get; set; } = string.Empty;
}
