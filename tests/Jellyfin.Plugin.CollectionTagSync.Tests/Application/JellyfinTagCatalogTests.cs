using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class JellyfinTagCatalogTests
{
    private static readonly BaseItemKind[] EligibleItemKinds =
        [BaseItemKind.Movie, BaseItemKind.Series];
    private static readonly string[] ExpectedTags = ["Blooth", "Kid-Approved", "Waltney"];

    [Fact]
    public void PickerReturnsDirectMovieAndSeriesTagsWithCaseEquivalentValuesCollapsed()
    {
        BaseItem[] items =
        [
            new Movie { Tags = [" Kid-Approved ", "Waltney"] },
            new Series { Tags = ["kid-approved", "Blooth", ""] },
        ];
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemList(It.Is<InternalItemsQuery>(query =>
                query.Recursive
                && query.IncludeItemTypes.SequenceEqual(EligibleItemKinds))))
            .Returns(items);
        var catalog = new JellyfinTagCatalog(libraryManager.Object);

        var result = catalog.GetPickerEntries();

        Assert.Equal(ExpectedTags, result);
        libraryManager.VerifyAll();
    }
}
