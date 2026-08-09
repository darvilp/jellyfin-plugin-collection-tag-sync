using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class JellyfinPlanWriterTests
{
    [Fact]
    public async Task AppliesMovieTagRemovalAndCollectionAddition()
    {
        var itemId = new Guid("94a202e5-d731-46d9-a269-66790dc85d59");
        var targetCollectionId = new Guid("b2048597-6da0-44fa-be94-53da90668dd5");
        var movie = new Movie
        {
            Id = itemId,
            Tags = ["Kid-Approved", "KID-APPROVED", "Waltney"],
        };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById(itemId)).Returns(movie);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(
                movie,
                It.IsAny<MediaBrowser.Controller.Entities.BaseItem>(),
                ItemUpdateType.MetadataEdit,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var collectionManager = new Mock<ICollectionManager>(MockBehavior.Strict);
        collectionManager
            .Setup(manager => manager.AddToCollectionAsync(
                targetCollectionId,
                It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { itemId }))))
            .Returns(Task.CompletedTask);
        var writer = new JellyfinPlanWriter(libraryManager.Object, collectionManager.Object);
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("kid-approved"),
                    [new TagNodeDefinition("Never")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(targetCollectionId, "Animation"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var plan = ReconciliationPlanner.Plan(
            configuration,
            new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                movie.Tags,
                directCollectionIds: []));

        await writer.ApplyAsync(plan, CancellationToken.None).ConfigureAwait(true);

        Assert.DoesNotContain(movie.Tags, tag =>
            StringComparer.OrdinalIgnoreCase.Equals(tag, "kid-approved"));
        Assert.Contains("Waltney", movie.Tags);
        libraryManager.VerifyAll();
        collectionManager.VerifyAll();
    }

    [Fact]
    public async Task AppliesSeriesCollectionRemovalAndConfiguredTagAddition()
    {
        var itemId = new Guid("3fb61400-b51a-444a-b51b-cd635cfa6e43");
        var sourceCollectionId = new Guid("d543a6b6-4697-4d85-a718-3e3b47784bed");
        var targetCollectionId = new Guid("14646fe5-35f4-47b4-af48-2322a2e844a9");
        var series = new Series
        {
            Id = itemId,
            Tags = [],
        };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById(itemId)).Returns(series);
        libraryManager
            .Setup(manager => manager.UpdateItemAsync(
                series,
                It.IsAny<MediaBrowser.Controller.Entities.BaseItem>(),
                ItemUpdateType.MetadataEdit,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var collectionManager = new Mock<ICollectionManager>(MockBehavior.Strict);
        collectionManager
            .Setup(manager => manager.RemoveFromCollectionAsync(
                targetCollectionId,
                It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { itemId }))))
            .Returns(Task.CompletedTask);
        var writer = new JellyfinPlanWriter(libraryManager.Object, collectionManager.Object);
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(targetCollectionId, "Old"),
                    [new TagNodeDefinition("Never")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new TagNodeDefinition("Blooth"),
                    [new CollectionNodeDefinition(sourceCollectionId, "Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var plan = ReconciliationPlanner.Plan(
            configuration,
            new ObservedItemState(
                itemId,
                EligibleItemKind.Series,
                directTags: [],
                directCollectionIds: [sourceCollectionId, targetCollectionId]));

        await writer.ApplyAsync(plan, CancellationToken.None).ConfigureAwait(true);

        Assert.Contains("Blooth", series.Tags);
        libraryManager.VerifyAll();
        collectionManager.VerifyAll();
    }
}
