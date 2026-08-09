using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Provides administrator-only privacy-safe plugin status and diagnostics.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/Status")]
public sealed class OperationalStatusController : ControllerBase
{
    private readonly IIncrementalReconciliationControl _incremental;
    private readonly FullReconcileRequestStore _fullReconcileRequests;
    private readonly MappingDiagnosticStore _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationalStatusController"/> class.
    /// </summary>
    /// <param name="incremental">The incremental coordinator control.</param>
    /// <param name="fullReconcileRequests">The broader-recovery request store.</param>
    /// <param name="diagnostics">The unresolved mapping diagnostic store.</param>
    public OperationalStatusController(
        IIncrementalReconciliationControl incremental,
        FullReconcileRequestStore fullReconcileRequests,
        MappingDiagnosticStore diagnostics)
    {
        _incremental = incremental;
        _fullReconcileRequests = fullReconcileRequests;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets current privacy-safe plugin status and diagnostics.</summary>
    /// <returns>The current status snapshot.</returns>
    [HttpGet]
    [ProducesResponseType<OperationalStatusResult>(StatusCodes.Status200OK)]
    public ActionResult<OperationalStatusResult> GetStatus()
    {
        var unresolved = _diagnostics.UnresolvedGroups.Select(group =>
            new UnresolvedMappingGroupStatus(
                group.GroupIndex,
                group.Target.DisplayLabel,
                group.MissingCollections.Select(collection =>
                    new CollectionPickerEntry(collection.Id, collection.DisplayName ?? string.Empty))));
        return Ok(new OperationalStatusResult(
            _incremental.Status,
            _fullReconcileRequests.Status,
            unresolved));
    }
}
