using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Provides administrator-only Full Reconcile status, preview, and confirmation APIs.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/FullReconcile")]
public sealed class FullReconcileController : ControllerBase
{
    private const string JellyfinUserIdClaim = "Jellyfin-UserId";
    private readonly FullReconcileStatusStore _statusStore;
    private readonly FullReconcileSafetyService _safetyService;
    private readonly FullReconcileApprovalService _approvalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileController"/> class.
    /// </summary>
    /// <param name="statusStore">The current Full Reconcile status store.</param>
    /// <param name="safetyService">The persisted preview and authorization boundary.</param>
    /// <param name="approvalService">The fresh confirmation execution boundary.</param>
    public FullReconcileController(
        FullReconcileStatusStore statusStore,
        FullReconcileSafetyService safetyService,
        FullReconcileApprovalService approvalService)
    {
        _statusStore = statusStore;
        _safetyService = safetyService;
        _approvalService = approvalService;
    }

    /// <summary>
    /// Gets the current or latest privacy-safe Full Reconcile status.
    /// </summary>
    /// <returns>The current status snapshot.</returns>
    [HttpGet("Status")]
    [ProducesResponseType<FullReconcileRunResult>(StatusCodes.Status200OK)]
    public ActionResult<FullReconcileRunResult> GetStatus()
    {
        return Ok(_statusStore.Current);
    }

    /// <summary>
    /// Creates one administrator-bound authorization for a persisted paused preview.
    /// </summary>
    /// <param name="runId">The paused run identity.</param>
    /// <returns>The persisted diagnostics and short-lived authorization.</returns>
    [HttpPost("{runId:guid}/Preview")]
    [ProducesResponseType<FullReconcilePreviewAuthorization>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<FullReconcilePreviewAuthorization> CreatePreviewAuthorization(Guid runId)
    {
        var administratorId = GetAdministratorId();
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var preview = _safetyService.CreatePreviewAuthorization(runId, administratorId);
        return preview is null ? NotFound() : Ok(preview);
    }

    /// <summary>
    /// Confirms one paused preview and waits for fresh serialized recomputation and conditional execution.
    /// </summary>
    /// <param name="runId">The paused run identity.</param>
    /// <param name="request">The opaque authorization request.</param>
    /// <param name="cancellationToken">Cancellation for the caller's wait only.</param>
    /// <returns>The fresh confirmation outcome.</returns>
    [HttpPost("{runId:guid}/Confirm")]
    [ProducesResponseType<FullReconcileConfirmationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<FullReconcileConfirmationResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FullReconcileConfirmationResult>> ConfirmAsync(
        Guid runId,
        [FromBody] FullReconcileConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var administratorId = GetAdministratorId();
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _approvalService
            .ConfirmAsync(runId, administratorId, request.Authorization, cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome == FullReconcileConfirmationOutcome.Accepted
            ? Ok(result)
            : Conflict(result);
    }

    private Guid GetAdministratorId()
    {
        var value = User.Claims.FirstOrDefault(claim => string.Equals(
            claim.Type,
            JellyfinUserIdClaim,
            StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(value, out var administratorId) ? administratorId : Guid.Empty;
    }
}
