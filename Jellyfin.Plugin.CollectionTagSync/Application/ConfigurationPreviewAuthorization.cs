using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Returns one complete candidate plan with a short-lived confirmation authorization.
/// </summary>
public sealed class ConfigurationPreviewAuthorization
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationPreviewAuthorization"/> class.
    /// </summary>
    /// <param name="preview">The non-executable candidate plan.</param>
    /// <param name="authorization">The opaque single-use authorization.</param>
    /// <param name="expiresAtUtc">The authorization expiry.</param>
    internal ConfigurationPreviewAuthorization(
        ConfigurationPlanPreview preview,
        string authorization,
        DateTimeOffset expiresAtUtc)
    {
        Preview = preview;
        Authorization = authorization;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the complete non-executable candidate plan.</summary>
    public ConfigurationPlanPreview Preview { get; }

    /// <summary>Gets the opaque single-use authorization.</summary>
    public string Authorization { get; }

    /// <summary>Gets the authorization expiry.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}
