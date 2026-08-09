using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Provides administrator-only direct tag discovery for the UI picker.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CollectionTagSync/Tags")]
public sealed class TagsController : ControllerBase
{
    private readonly ITagCatalog _tagCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagsController"/> class.
    /// </summary>
    /// <param name="tagCatalog">The direct tag discovery boundary.</param>
    public TagsController(ITagCatalog tagCatalog)
    {
        _tagCatalog = tagCatalog;
    }

    /// <summary>Gets current tag picker entries.</summary>
    /// <returns>Normalized current direct tag spellings.</returns>
    [HttpGet("Picker")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetPickerEntries()
    {
        return Ok(_tagCatalog.GetPickerEntries());
    }
}
