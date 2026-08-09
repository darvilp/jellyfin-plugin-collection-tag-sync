using Jellyfin.Plugin.CollectionTagSync.Configuration;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Carries one complete candidate and its opaque preview authorization.
/// </summary>
public sealed class ConfigurationConfirmationRequest
{
    /// <summary>Gets or sets the complete candidate configuration.</summary>
    public PluginConfiguration Candidate { get; set; } = new();

    /// <summary>Gets or sets the opaque preview authorization.</summary>
    public string Authorization { get; set; } = string.Empty;
}
