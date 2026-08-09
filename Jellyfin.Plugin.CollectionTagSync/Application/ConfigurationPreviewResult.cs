using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the server-authoritative result of one candidate preview request.
/// </summary>
public sealed class ConfigurationPreviewResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationPreviewResult"/> class.
    /// </summary>
    /// <param name="outcome">The preview outcome.</param>
    /// <param name="activeRevision">The active configuration revision used for validation.</param>
    /// <param name="authorization">The complete preview and authorization, when ready.</param>
    /// <param name="validationErrors">Server-side candidate validation errors.</param>
    internal ConfigurationPreviewResult(
        ConfigurationPreviewOutcome outcome,
        long activeRevision,
        ConfigurationPreviewAuthorization? authorization,
        IEnumerable<ConfigurationActivationError> validationErrors)
    {
        Outcome = outcome;
        ActiveRevision = activeRevision;
        Authorization = authorization;
        ValidationErrors = [.. validationErrors];
    }

    /// <summary>Gets the preview outcome.</summary>
    public ConfigurationPreviewOutcome Outcome { get; }

    /// <summary>Gets the active configuration revision used for validation.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the complete preview and authorization, when ready.</summary>
    public ConfigurationPreviewAuthorization? Authorization { get; }

    /// <summary>Gets server-side candidate validation errors.</summary>
    public IReadOnlyList<ConfigurationActivationError> ValidationErrors { get; }
}
