using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class JellyfinItemStateReaderTests
{
    [Fact]
    public void GetsCurrentItemTitleAndReturnsEmptyForMissingItem()
    {
        var itemId = new Guid("ec18b7c5-6fcf-4521-84df-745b619542f1");
        var missingId = new Guid("ef33c0ea-764f-4ae8-976b-f229243afeb5");
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.GetItemById(itemId))
            .Returns(new Movie { Id = itemId, Name = "Waltney Adventure" });
        libraryManager.Setup(manager => manager.GetItemById(missingId)).Returns((BaseItem?)null);
        var reader = new JellyfinItemStateReader(libraryManager.Object);

        Assert.Equal("Waltney Adventure", reader.GetTitle(itemId));
        Assert.Equal(string.Empty, reader.GetTitle(missingId));
        libraryManager.VerifyAll();
    }

    [Fact]
    public async Task ReadsOnlyDirectMovieAndSeriesTags()
    {
        var movie = new Movie
        {
            Id = new Guid("bfe24f11-9c77-4840-b8b7-57f42a435c00"),
            Tags = ["Kid-Approved", "kid-approved"],
        };
        var series = new Series
        {
            Id = new Guid("a80700cf-aa54-47af-bca1-5fa74b1a6ecf"),
            Tags = ["Blooth"],
        };
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.GetItemById(movie.Id)).Returns(movie);
        libraryManager.Setup(manager => manager.GetItemById(series.Id)).Returns(series);
        var reader = new JellyfinItemStateReader(libraryManager.Object);
        var configuration = TagOnlyConfiguration();

        var movieState = await reader
            .ReadAsync(movie.Id, configuration, CancellationToken.None)
            .ConfigureAwait(true);
        var seriesState = await reader
            .ReadAsync(series.Id, configuration, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(EligibleItemKind.Movie, Assert.IsType<ObservedItemState>(movieState).ItemKind);
        Assert.Equal(movie.Tags, movieState.DirectTags);
        Assert.Equal(EligibleItemKind.Series, Assert.IsType<ObservedItemState>(seriesState).ItemKind);
        Assert.Equal(series.Tags, seriesState.DirectTags);
        libraryManager.VerifyAll();
    }

    [Fact]
    public async Task IgnoresEpisodeSeasonAndAudioItems()
    {
        BaseItem[] ineligibleItems =
        [
            new Episode { Id = new Guid("443e9b89-7662-4023-aa75-cd93df8efdd9") },
            new Season { Id = new Guid("e2fa9f41-cfc4-481f-8e65-456c04b013c9") },
            new Audio { Id = new Guid("027289b0-21de-49e3-989d-cbe2be1fb099") },
        ];
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        foreach (var item in ineligibleItems)
        {
            libraryManager.Setup(manager => manager.GetItemById(item.Id)).Returns(item);
        }

        var reader = new JellyfinItemStateReader(libraryManager.Object);
        var configuration = TagOnlyConfiguration();

        foreach (var item in ineligibleItems)
        {
            var state = await reader
                .ReadAsync(item.Id, configuration, CancellationToken.None)
                .ConfigureAwait(true);
            Assert.Null(state);
        }

        libraryManager.VerifyAll();
    }

    private static MappingConfiguration TagOnlyConfiguration()
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Kid-Approved"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
    }
}
