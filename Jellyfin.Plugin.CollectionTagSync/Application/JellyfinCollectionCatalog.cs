using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Lists and creates GUID-backed collections through Jellyfin services.
/// </summary>
internal sealed class JellyfinCollectionCatalog : ICollectionCatalog
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinCollectionCatalog"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="collectionManager">The Jellyfin collection manager.</param>
    public JellyfinCollectionCatalog(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<CollectionPickerEntry> GetPickerEntries()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
            Recursive = true,
        }).OfType<BoxSet>()
            .Select(ToPickerEntry)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<CollectionPickerEntry> CreateAsync(string displayName)
    {
        var collection = await _collectionManager
            .CreateCollectionAsync(new CollectionCreationOptions
            {
                Name = displayName,
                IsLocked = false,
                ItemIdList = [],
            })
            .ConfigureAwait(false);
        return ToPickerEntry(collection);
    }

    private static CollectionPickerEntry ToPickerEntry(BoxSet collection)
    {
        return new CollectionPickerEntry(collection.Id, collection.Name ?? string.Empty);
    }
}
