using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the server-authoritative result of one configuration activation request.
/// </summary>
public sealed class ConfigurationActivationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActivationResult"/> class.
    /// </summary>
    /// <param name="outcome">The activation outcome.</param>
    /// <param name="activeRevision">The still-active or newly accepted revision.</param>
    /// <param name="reconciliationId">The queued or paused reconciliation identity.</param>
    /// <param name="validationErrors">Validation errors, if any.</param>
    internal ConfigurationActivationResult(
        ConfigurationActivationOutcome outcome,
        long activeRevision,
        Guid? reconciliationId,
        IEnumerable<ConfigurationActivationError> validationErrors)
    {
        Outcome = outcome;
        ActiveRevision = activeRevision;
        ReconciliationId = reconciliationId;
        ValidationErrors = Array.AsReadOnly([.. validationErrors]);
    }

    /// <summary>
    /// Gets the activation outcome.
    /// </summary>
    public ConfigurationActivationOutcome Outcome { get; }

    /// <summary>
    /// Gets the active configuration revision.
    /// </summary>
    public long ActiveRevision { get; }

    /// <summary>
    /// Gets the queued or paused reconciliation identity.
    /// </summary>
    public Guid? ReconciliationId { get; }

    /// <summary>
    /// Gets server-side validation errors.
    /// </summary>
    public IReadOnlyList<ConfigurationActivationError> ValidationErrors { get; }
}
