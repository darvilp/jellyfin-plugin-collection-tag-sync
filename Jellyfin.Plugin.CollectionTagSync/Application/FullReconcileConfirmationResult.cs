namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reports one paused Full Reconcile confirmation attempt.
/// </summary>
public sealed class FullReconcileConfirmationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileConfirmationResult"/> class.
    /// </summary>
    /// <param name="outcome">The confirmation outcome.</param>
    /// <param name="runResult">The fresh run result, when authorization reached recomputation.</param>
    internal FullReconcileConfirmationResult(
        FullReconcileConfirmationOutcome outcome,
        FullReconcileRunResult? runResult)
    {
        Outcome = outcome;
        RunResult = runResult;
    }

    /// <summary>Gets the confirmation outcome.</summary>
    public FullReconcileConfirmationOutcome Outcome { get; }

    /// <summary>Gets the fresh run result, when authorization reached recomputation.</summary>
    public FullReconcileRunResult? RunResult { get; }
}
