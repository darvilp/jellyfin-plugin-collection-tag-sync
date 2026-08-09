using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reads direct Movie and Series state through Jellyfin services.
/// </summary>
internal sealed class JellyfinItemStateReader : IItemStateReader
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinItemStateReader"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public JellyfinItemStateReader(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public Task<ObservedItemState?> ReadAsync(
        Guid itemId,
        MappingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _libraryManager.GetItemById(itemId);
        var itemKind = item switch
        {
            Movie => EligibleItemKind.Movie,
            Series => EligibleItemKind.Series,
            _ => (EligibleItemKind?)null,
        };
        if (item is null || itemKind is null)
        {
            return Task.FromResult<ObservedItemState?>(null);
        }

        var collectionIds = configuration.Groups
            .Where(group => group.IsEnabled)
            .SelectMany(group => group.Sources.Append(group.Target))
            .OfType<CollectionNode>()
            .Distinct()
            .Where(collection => IsMember(collection.Id, itemId))
            .Select(collection => collection.Id);
        return Task.FromResult<ObservedItemState?>(new ObservedItemState(
            itemId,
            itemKind.Value,
            item.Tags,
            collectionIds));
    }

    private bool IsMember(Guid collectionId, Guid itemId)
    {
        return _libraryManager.GetItemById(collectionId) is BoxSet collection
            && collection.GetLinkedChildren().Any(child => child.Id == itemId);
    }
}
