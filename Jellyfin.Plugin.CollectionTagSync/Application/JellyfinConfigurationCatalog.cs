using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Enumerates eligible items and resolves collection identities through Jellyfin.
/// </summary>
internal sealed class JellyfinConfigurationCatalog : IConfigurationCatalog
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinConfigurationCatalog"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public JellyfinConfigurationCatalog(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<Guid> GetEligibleItemIds()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
        }).Select(item => item.Id).ToArray();
    }

    /// <inheritdoc />
    public bool CollectionExists(Guid collectionId)
    {
        return _libraryManager.GetItemById(collectionId) is BoxSet;
    }
}
