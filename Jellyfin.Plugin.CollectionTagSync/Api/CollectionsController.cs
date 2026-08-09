using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Provides administrator-only GUID picker and independent Add New collection APIs.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/Collections")]
public sealed class CollectionsController : ControllerBase
{
    private readonly CollectionSelectionService _selectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionsController"/> class.
    /// </summary>
    /// <param name="selectionService">The GUID-only collection selection boundary.</param>
    public CollectionsController(CollectionSelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    /// <summary>Gets every current GUID-backed collection picker choice.</summary>
    /// <returns>Distinct current picker entries.</returns>
    [HttpGet("Picker")]
    [ProducesResponseType<IReadOnlyList<CollectionPickerEntry>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CollectionPickerEntry>> GetPickerEntries()
    {
        return Ok(_selectionService.GetPickerEntries());
    }

    /// <summary>Performs the distinct Add New collection action immediately.</summary>
    /// <param name="request">The proposed display name.</param>
    /// <param name="cancellationToken">Cancellation before independent creation begins.</param>
    /// <returns>The selected created GUID or explicit duplicate recovery entries.</returns>
    [HttpPost("Create")]
    [ProducesResponseType<CollectionCreationResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<CollectionCreationResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CollectionCreationResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CollectionCreationResult>> CreateAsync(
        [FromBody] CollectionCreationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _selectionService
            .CreateAsync(request.Name, cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome switch
        {
            CollectionCreationOutcome.Created => StatusCode(StatusCodes.Status201Created, result),
            CollectionCreationOutcome.InvalidName => BadRequest(result),
            CollectionCreationOutcome.DuplicateName => Conflict(result),
            _ => throw new InvalidOperationException("Unknown collection creation outcome."),
        };
    }
}
