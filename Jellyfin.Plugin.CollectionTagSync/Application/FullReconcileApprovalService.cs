using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Consumes paused-plan authorizations and requests fresh serialized execution.
/// </summary>
public sealed class FullReconcileApprovalService
{
    private readonly FullReconcileRequestStore _requestStore;
    private readonly FullReconcileSafetyService _safetyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileApprovalService"/> class.
    /// </summary>
    /// <param name="requestStore">The serialized Full Reconcile request boundary.</param>
    /// <param name="safetyService">The preview authorization boundary.</param>
    public FullReconcileApprovalService(
        FullReconcileRequestStore requestStore,
        FullReconcileSafetyService safetyService)
    {
        _requestStore = requestStore;
        _safetyService = safetyService;
    }

    /// <summary>
    /// Recomputes and conditionally executes one authorized paused Full Reconcile.
    /// </summary>
    /// <param name="pausedRunId">The paused run identity.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="authorization">The opaque preview authorization.</param>
    /// <param name="cancellationToken">Cancellation for the caller's wait only.</param>
    /// <returns>The authorization and fresh-plan outcome.</returns>
    public async Task<FullReconcileConfirmationResult> ConfirmAsync(
        Guid pausedRunId,
        Guid administratorId,
        string authorization,
        CancellationToken cancellationToken)
    {
        var confirmation = _safetyService.ConsumeAuthorization(
            pausedRunId,
            administratorId,
            authorization);
        if (confirmation is null)
        {
            return new FullReconcileConfirmationResult(
                FullReconcileConfirmationOutcome.InvalidAuthorization,
                null);
        }

        var runResult = await _requestStore
            .RequestConfirmedAsync(confirmation, cancellationToken)
            .ConfigureAwait(false);
        var outcome = runResult.State == FullReconcileState.AwaitingApproval
            ? FullReconcileConfirmationOutcome.StalePreview
            : FullReconcileConfirmationOutcome.Accepted;
        return new FullReconcileConfirmationResult(outcome, runResult);
    }
}
