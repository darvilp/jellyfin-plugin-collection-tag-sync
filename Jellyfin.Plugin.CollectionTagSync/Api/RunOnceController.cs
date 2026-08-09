using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Provides administrator-only run-once preview, confirmation, and status APIs.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/RunOnce")]
public sealed class RunOnceController : ControllerBase
{
    private readonly RunOnceService _runOnceService;
    private readonly BackgroundReconciliationStatusStore _statusStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceController"/> class.
    /// </summary>
    /// <param name="runOnceService">The run-once orchestration service.</param>
    /// <param name="statusStore">The privacy-safe background status store.</param>
    public RunOnceController(
        RunOnceService runOnceService,
        BackgroundReconciliationStatusStore statusStore)
    {
        _runOnceService = runOnceService;
        _statusStore = statusStore;
    }

    /// <summary>
    /// Calculates one complete run-once plan and creates a short-lived authorization.
    /// </summary>
    /// <param name="operation">The operation and ephemeral exclusions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete non-executable preview and authorization.</returns>
    [HttpPost("Preview")]
    [ProducesResponseType<RunOncePreviewResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RunOncePreviewResult>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunOncePreviewResult>> PreviewAsync(
        [FromBody] RunOnceOperationRequest operation,
        CancellationToken cancellationToken)
    {
        var administratorId = AdministratorIdentity.Get(User);
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _runOnceService
            .PreviewAsync(operation, administratorId, cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome == RunOncePreviewOutcome.Ready
            ? Ok(result)
            : BadRequest(result);
    }

    /// <summary>
    /// Recomputes and conditionally queues one previously previewed run-once request.
    /// </summary>
    /// <param name="request">The exact operation, exclusions, and opaque authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An accepted, validation, or new-preview-required result.</returns>
    [HttpPost("Confirm")]
    [ProducesResponseType<RunOnceExecutionResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RunOnceExecutionResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RunOnceExecutionResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RunOnceExecutionResult>> ConfirmAsync(
        [FromBody] RunOnceConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var administratorId = AdministratorIdentity.Get(User);
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _runOnceService
            .ConfirmAsync(
                request.Operation,
                administratorId,
                request.Authorization,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome switch
        {
            RunOnceExecutionOutcome.Accepted => Accepted(result),
            RunOnceExecutionOutcome.Invalid => BadRequest(result),
            RunOnceExecutionOutcome.RequiresPreview => Conflict(result),
            RunOnceExecutionOutcome.InvalidAuthorization => Conflict(result),
            _ => throw new InvalidOperationException("Unknown run-once confirmation outcome."),
        };
    }

    /// <summary>
    /// Gets privacy-safe background run-once reconciliation status.
    /// </summary>
    /// <param name="id">The opaque reconciliation identity.</param>
    /// <returns>The status or not found.</returns>
    [HttpGet("Reconciliations/{id:guid}")]
    [ProducesResponseType<BackgroundReconciliationStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BackgroundReconciliationStatus> GetReconciliationStatus(Guid id)
    {
        var status = _statusStore.Get(id);
        return status is null ? NotFound() : Ok(status);
    }
}
