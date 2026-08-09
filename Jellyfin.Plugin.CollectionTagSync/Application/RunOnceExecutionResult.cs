using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes the server-authoritative result of one run-once confirmation.
/// </summary>
public sealed class RunOnceExecutionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceExecutionResult"/> class.
    /// </summary>
    /// <param name="outcome">The confirmation outcome.</param>
    /// <param name="activeRevision">The unchanged active configuration revision.</param>
    /// <param name="reconciliationId">The queued background reconciliation identity.</param>
    /// <param name="validationErrors">Server-side validation errors.</param>
    internal RunOnceExecutionResult(
        RunOnceExecutionOutcome outcome,
        long activeRevision,
        Guid? reconciliationId,
        IEnumerable<RunOnceValidationError> validationErrors)
    {
        Outcome = outcome;
        ActiveRevision = activeRevision;
        ReconciliationId = reconciliationId;
        ValidationErrors = [.. validationErrors];
    }

    /// <summary>Gets the confirmation outcome.</summary>
    public RunOnceExecutionOutcome Outcome { get; }

    /// <summary>Gets the unchanged active configuration revision.</summary>
    public long ActiveRevision { get; }

    /// <summary>Gets the queued background reconciliation identity.</summary>
    public Guid? ReconciliationId { get; }

    /// <summary>Gets server-side validation errors.</summary>
    public IReadOnlyList<RunOnceValidationError> ValidationErrors { get; }
}
