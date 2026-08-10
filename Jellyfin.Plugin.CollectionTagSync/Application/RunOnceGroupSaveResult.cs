using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Configuration;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reports server validation and the persisted reusable run-once group.
/// </summary>
public sealed class RunOnceGroupSaveResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceGroupSaveResult"/> class.
    /// </summary>
    /// <param name="outcome">The save outcome.</param>
    /// <param name="group">The server-normalized persisted group.</param>
    /// <param name="validationErrors">Server validation failures.</param>
    public RunOnceGroupSaveResult(
        RunOnceGroupSaveOutcome outcome,
        RunOnceGroupConfiguration? group,
        IEnumerable<RunOnceValidationError> validationErrors)
    {
        Outcome = outcome;
        Group = group;
        ValidationErrors = [.. validationErrors];
    }

    /// <summary>Gets the save outcome.</summary>
    public RunOnceGroupSaveOutcome Outcome { get; }

    /// <summary>Gets the persisted group when the save succeeded.</summary>
    public RunOnceGroupConfiguration? Group { get; }

    /// <summary>Gets server validation failures.</summary>
    public IReadOnlyList<RunOnceValidationError> ValidationErrors { get; }
}
