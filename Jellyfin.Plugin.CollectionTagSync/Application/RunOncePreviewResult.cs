using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the server-authoritative result of one run-once preview request.
/// </summary>
public sealed class RunOncePreviewResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOncePreviewResult"/> class.
    /// </summary>
    /// <param name="outcome">The preview outcome.</param>
    /// <param name="activeRevision">The active revision used during planning.</param>
    /// <param name="authorization">The complete preview authorization, when ready.</param>
    /// <param name="validationErrors">Server-side validation errors.</param>
    internal RunOncePreviewResult(
        RunOncePreviewOutcome outcome,
        long activeRevision,
        RunOncePreviewAuthorization? authorization,
        IEnumerable<RunOnceValidationError> validationErrors)
    {
        Outcome = outcome;
        ActiveRevision = activeRevision;
        Authorization = authorization;
        ValidationErrors = [.. validationErrors];
    }

    /// <summary>Gets the preview outcome.</summary>
    public RunOncePreviewOutcome Outcome { get; }

    /// <summary>Gets the active revision used during planning.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the complete preview authorization, when ready.</summary>
    public RunOncePreviewAuthorization? Authorization { get; }

    /// <summary>Gets server-side validation errors.</summary>
    public IReadOnlyList<RunOnceValidationError> ValidationErrors { get; }
}
