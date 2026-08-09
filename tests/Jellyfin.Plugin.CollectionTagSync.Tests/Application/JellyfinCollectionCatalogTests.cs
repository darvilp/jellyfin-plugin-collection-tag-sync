using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class JellyfinCollectionCatalogTests
{
    [Fact]
    public void PickerReadsCurrentNamesAndPreservesDistinctDuplicateGuids()
    {
        var first = new BoxSet
        {
            Id = new Guid("3f7bb2e4-88aa-4624-bbc8-f68766598142"),
            Name = "Animation",
        };
        var second = new BoxSet
        {
            Id = new Guid("b885f1cc-7ad6-4ea6-bef6-407271ccf63d"),
            Name = "Animation",
        };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemList(It.Is<InternalItemsQuery>(query =>
                query.Recursive
                && query.IncludeItemTypes.SequenceEqual(new[] { BaseItemKind.BoxSet }))))
            .Returns([first, second]);
        var catalog = new JellyfinCollectionCatalog(
            libraryManager.Object,
            Mock.Of<ICollectionManager>());

        var initial = catalog.GetPickerEntries();

        Assert.Equal(2, initial.Count);
        Assert.Contains(initial, entry => entry.Id == first.Id && entry.DisplayName == "Animation");
        Assert.Contains(initial, entry => entry.Id == second.Id && entry.DisplayName == "Animation");

        first.Name = "Animated Movies";
        var renamed = catalog.GetPickerEntries();

        Assert.Contains(renamed, entry => entry.Id == first.Id && entry.DisplayName == "Animated Movies");
        libraryManager.VerifyAll();
    }

    [Fact]
    public async Task CreationUsesJellyfinReturnedIdentityAndNoInitialMembers()
    {
        var created = new BoxSet
        {
            Id = new Guid("07ac3e84-d21a-4fb9-ac1b-b94642e3a69f"),
            Name = "Waltney Picks",
        };
        var collectionManager = new Mock<ICollectionManager>(MockBehavior.Strict);
        collectionManager
            .Setup(manager => manager.CreateCollectionAsync(It.Is<CollectionCreationOptions>(options =>
                options.Name == "Waltney Picks"
                && !options.IsLocked
                && options.ItemIdList.Count == 0)))
            .ReturnsAsync(created);
        var catalog = new JellyfinCollectionCatalog(
            Mock.Of<ILibraryManager>(),
            collectionManager.Object);

        var result = await catalog.CreateAsync("Waltney Picks").ConfigureAwait(true);

        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Name, result.DisplayName);
        collectionManager.VerifyAll();
    }

    [Fact]
    public async Task AdapterPreservesPathDerivedIdentityReturnedForRepeatedSameNameCreation()
    {
        var reusedId = new Guid("a0c83e13-b3d4-4c09-922f-77b8fbedcd1b");
        var collectionManager = new Mock<ICollectionManager>(MockBehavior.Strict);
        collectionManager
            .SetupSequence(manager => manager.CreateCollectionAsync(
                It.Is<CollectionCreationOptions>(options => options.Name == "Blooth Picks")))
            .ReturnsAsync(new BoxSet { Id = reusedId, Name = "Blooth Picks" })
            .ReturnsAsync(new BoxSet { Id = reusedId, Name = "Blooth Picks" });
        var catalog = new JellyfinCollectionCatalog(
            Mock.Of<ILibraryManager>(),
            collectionManager.Object);

        var first = await catalog.CreateAsync("Blooth Picks").ConfigureAwait(true);
        var second = await catalog.CreateAsync("Blooth Picks").ConfigureAwait(true);

        Assert.Equal(reusedId, first.Id);
        Assert.Equal(first.Id, second.Id);
        collectionManager.VerifyAll();
    }
}
