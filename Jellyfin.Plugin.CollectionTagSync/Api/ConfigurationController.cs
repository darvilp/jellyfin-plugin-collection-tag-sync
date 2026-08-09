using System;
using System.Linq;
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
/// Provides administrator-only continuous configuration activation and status APIs.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/Configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private const string JellyfinUserIdClaim = "Jellyfin-UserId";
    private readonly ConfigurationActivationService _activationService;
    private readonly BackgroundReconciliationStatusStore _statusStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
    /// </summary>
    /// <param name="activationService">The configuration activation service.</param>
    /// <param name="statusStore">The background reconciliation status store.</param>
    public ConfigurationController(
        ConfigurationActivationService activationService,
        BackgroundReconciliationStatusStore statusStore)
    {
        _activationService = activationService;
        _statusStore = statusStore;
    }

    /// <summary>
    /// Validates and activates one complete ordinary configuration candidate.
    /// </summary>
    /// <param name="candidate">The complete candidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An accepted, validation, or preview-required result.</returns>
    [HttpPost]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfigurationActivationResult>> ActivateAsync(
        [FromBody] PluginConfiguration candidate,
        CancellationToken cancellationToken)
    {
        var result = await _activationService.ActivateAsync(candidate, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ConfigurationActivationOutcome.Accepted => Accepted(result),
            ConfigurationActivationOutcome.Invalid => BadRequest(result),
            ConfigurationActivationOutcome.RequiresPreview => Conflict(result),
            _ => throw new InvalidOperationException("Unknown configuration activation outcome."),
        };
    }

    /// <summary>
    /// Calculates one complete candidate plan and creates a short-lived authorization.
    /// </summary>
    /// <param name="candidate">The complete candidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete non-executable preview and authorization.</returns>
    [HttpPost("Preview")]
    [ProducesResponseType<ConfigurationPreviewResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ConfigurationPreviewResult>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfigurationPreviewResult>> PreviewAsync(
        [FromBody] PluginConfiguration candidate,
        CancellationToken cancellationToken)
    {
        var administratorId = GetAdministratorId();
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _activationService
            .PreviewAsync(candidate, administratorId, cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome == ConfigurationPreviewOutcome.Ready
            ? Ok(result)
            : BadRequest(result);
    }

    /// <summary>
    /// Recomputes and conditionally activates one previously previewed candidate.
    /// </summary>
    /// <param name="request">The complete candidate and opaque authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An accepted, validation, or new-preview-required result.</returns>
    [HttpPost("Confirm")]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ConfigurationActivationResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfigurationActivationResult>> ConfirmAsync(
        [FromBody] ConfigurationConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var administratorId = GetAdministratorId();
        if (administratorId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _activationService
            .ConfirmAsync(
                request.Candidate,
                administratorId,
                request.Authorization,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome switch
        {
            ConfigurationActivationOutcome.Accepted => Accepted(result),
            ConfigurationActivationOutcome.Invalid => BadRequest(result),
            ConfigurationActivationOutcome.RequiresPreview => Conflict(result),
            ConfigurationActivationOutcome.InvalidAuthorization => Conflict(result),
            _ => throw new InvalidOperationException("Unknown configuration confirmation outcome."),
        };
    }

    /// <summary>
    /// Gets privacy-safe background reconciliation status.
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

    private Guid GetAdministratorId()
    {
        var value = User.Claims.FirstOrDefault(claim => string.Equals(
            claim.Type,
            JellyfinUserIdClaim,
            StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(value, out var administratorId) ? administratorId : Guid.Empty;
    }
}
