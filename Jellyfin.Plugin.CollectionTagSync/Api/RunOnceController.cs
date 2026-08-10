using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
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
    /// Gets all persisted reusable run-once groups.
    /// </summary>
    /// <returns>Independent group snapshots in administrator-defined order.</returns>
    [HttpGet("Groups")]
    [ProducesResponseType<IReadOnlyList<RunOnceGroupConfiguration>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RunOnceGroupConfiguration>> GetGroups()
    {
        return Ok(_runOnceService.GetGroups());
    }

    /// <summary>
    /// Validates and persists one new or edited reusable run-once group.
    /// </summary>
    /// <param name="candidate">The complete candidate group.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted server-normalized group or validation failures.</returns>
    [HttpPost("Groups")]
    [ProducesResponseType<RunOnceGroupSaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<RunOnceGroupSaveResult>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunOnceGroupSaveResult>> SaveGroupAsync(
        [FromBody] RunOnceGroupConfiguration candidate,
        CancellationToken cancellationToken)
    {
        var result = await _runOnceService.SaveGroupAsync(candidate, cancellationToken).ConfigureAwait(false);
        return result.Outcome == RunOnceGroupSaveOutcome.Saved ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Deletes one persisted reusable run-once group.
    /// </summary>
    /// <param name="id">The stable group identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No content when deleted, or not found.</returns>
    [HttpDelete("Groups/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroupAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _runOnceService.DeleteGroupAsync(id, cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
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
        [FromBody] SavedRunOnceOperationRequest operation,
        CancellationToken cancellationToken)
    {
        var administratorId = AdministratorIdentity.Get(User);
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _runOnceService
            .PreviewSavedAsync(operation, administratorId, cancellationToken)
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
            .ConfirmSavedAsync(
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
