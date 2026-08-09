using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class CollectionSelectionServiceTests
{
    [Fact]
    public void PickerKeepsDuplicateNamesDistinctAndReflectsRenameByGuid()
    {
        var firstId = new Guid("f55e221d-bb6a-43fd-ae38-b11cdf99de9b");
        var secondId = new Guid("699ee04b-5b3c-49bc-8b96-83520337b308");
        var catalog = new RecordingCollectionCatalog(
            new CollectionPickerEntry(firstId, "Animation"),
            new CollectionPickerEntry(secondId, "Animation"));
        using var service = new CollectionSelectionService(catalog);

        var initial = service.GetPickerEntries();

        Assert.Equal(2, initial.Count);
        Assert.Contains(initial, entry => entry.Id == firstId && entry.DisplayName == "Animation");
        Assert.Contains(initial, entry => entry.Id == secondId && entry.DisplayName == "Animation");

        catalog.Rename(firstId, "Animated Movies");
        var renamed = service.GetPickerEntries();

        Assert.Contains(renamed, entry => entry.Id == firstId && entry.DisplayName == "Animated Movies");
        Assert.DoesNotContain(renamed, entry => entry.Id == firstId && entry.DisplayName == "Animation");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNameIsRejectedBeforeJellyfinCreation(string name)
    {
        var catalog = new RecordingCollectionCatalog();
        using var service = new CollectionSelectionService(catalog);

        var result = await service.CreateAsync(name, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(CollectionCreationOutcome.InvalidName, result.Outcome);
        Assert.Null(result.SelectedCollection);
        Assert.Empty(result.MatchingCollections);
        Assert.Equal(0, catalog.CreateCount);
    }

    [Fact]
    public async Task NormalizedDuplicateReturnsEveryMatchingPickerEntryWithoutCreating()
    {
        var firstId = new Guid("92d3568a-ed90-4126-8d5f-ce2863423099");
        var secondId = new Guid("8d36507c-9e4b-4b32-8c66-fbcbe0a27935");
        var catalog = new RecordingCollectionCatalog(
            new CollectionPickerEntry(firstId, "Animation"),
            new CollectionPickerEntry(secondId, " animation "),
            new CollectionPickerEntry(
                new Guid("230c0158-d86a-4506-847a-6b2d3f97efc4"),
                "Kids"));
        using var service = new CollectionSelectionService(catalog);

        var result = await service
            .CreateAsync("  ANIMATION  ", CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(CollectionCreationOutcome.DuplicateName, result.Outcome);
        Assert.Null(result.SelectedCollection);
        Assert.Equal(
            new[] { firstId, secondId }.Order(),
            result.MatchingCollections.Select(entry => entry.Id).Order());
        Assert.Equal(0, catalog.CreateCount);
    }

    [Fact]
    public async Task SuccessfulCreationReturnsNewGuidAsSelectedPickerValue()
    {
        var createdId = new Guid("2e907932-8a1a-4583-b19e-d3b21e0ea70c");
        var catalog = new RecordingCollectionCatalog
        {
            NextCreatedId = createdId,
        };
        using var service = new CollectionSelectionService(catalog);

        var result = await service
            .CreateAsync("  Waltney Picks  ", CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(CollectionCreationOutcome.Created, result.Outcome);
        var selected = Assert.IsType<CollectionPickerEntry>(result.SelectedCollection);
        Assert.Equal(createdId, selected.Id);
        Assert.Equal("Waltney Picks", selected.DisplayName);
        Assert.Contains(service.GetPickerEntries(), entry => entry.Id == createdId);
        Assert.Equal(1, catalog.CreateCount);
    }

    [Fact]
    public async Task ConcurrentNormalizedNamesCreateAtMostOneCollection()
    {
        var catalog = new RecordingCollectionCatalog();
        using var service = new CollectionSelectionService(catalog);

        var results = await Task.WhenAll(
            service.CreateAsync("Blooth Picks", CancellationToken.None),
            service.CreateAsync(" blooth picks ", CancellationToken.None)).ConfigureAwait(true);

        Assert.Single(results, result => result.Outcome == CollectionCreationOutcome.Created);
        Assert.Single(results, result => result.Outcome == CollectionCreationOutcome.DuplicateName);
        Assert.Equal(1, catalog.CreateCount);
    }

    [Fact]
    public async Task CreatedCollectionRemainsWhenLaterRunOnceWorkflowIsCanceled()
    {
        var catalog = new RecordingCollectionCatalog();
        using var selection = new CollectionSelectionService(catalog);
        var created = Assert.IsType<CollectionPickerEntry>((await selection
            .CreateAsync("Waltney Picks", CancellationToken.None)
            .ConfigureAwait(true)).SelectedCollection);
        var statusStore = new BackgroundReconciliationStatusStore();
        using var runOnce = new RunOnceService(
            new FixedPersistence(new PluginConfiguration { Revision = 1 }),
            catalog,
            new NullStateReader(),
            new ConfigurationReconciliationDispatcher(statusStore),
            new ReconciliationExecutionGate(),
            TimeProvider.System);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runOnce.PreviewAsync(
            new RunOnceOperationRequest
            {
                Target = Collection(created.Id, created.DisplayName),
                Sources = [Tag("Waltney")],
                Policy = MappingPolicy.Additive,
            },
            new Guid("75b34ccc-6877-41fc-a0f7-b256b7803c29"),
            canceled.Token)).ConfigureAwait(true);

        Assert.Contains(selection.GetPickerEntries(), entry => entry.Id == created.Id);
        Assert.Equal(1, catalog.CreateCount);
    }

    [Fact]
    public async Task CreatedCollectionRemainsWhenLaterConfigurationSaveFails()
    {
        var catalog = new RecordingCollectionCatalog();
        using var selection = new CollectionSelectionService(catalog);
        var created = Assert.IsType<CollectionPickerEntry>((await selection
            .CreateAsync("Blooth Picks", CancellationToken.None)
            .ConfigureAwait(true)).SelectedCollection);
        var statusStore = new BackgroundReconciliationStatusStore();
        using var activation = new ConfigurationActivationService(
            new ThrowingPersistence(new PluginConfiguration { Revision = 3 }),
            catalog,
            new NullStateReader(),
            new ConfigurationReconciliationDispatcher(statusStore),
            statusStore,
            new ReconciliationExecutionGate(),
            TimeProvider.System);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                new MappingGroupConfiguration
                {
                    Target = Collection(created.Id, created.DisplayName),
                    Sources = [Tag("Blooth")],
                    Policy = MappingPolicy.Additive,
                    IsEnabled = true,
                },
            ],
        };

        await Assert.ThrowsAsync<IOException>(() => activation
            .ActivateAsync(candidate, CancellationToken.None)).ConfigureAwait(true);

        Assert.Contains(selection.GetPickerEntries(), entry => entry.Id == created.Id);
        Assert.Equal(1, catalog.CreateCount);
    }

    private static MappingNodeConfiguration Tag(string value)
    {
        return new MappingNodeConfiguration
        {
            Kind = MappingNodeKind.Tag,
            TagValue = value,
        };
    }

    private static MappingNodeConfiguration Collection(Guid id, string name)
    {
        return new MappingNodeConfiguration
        {
            Kind = MappingNodeKind.Collection,
            CollectionId = id,
            CollectionDisplayName = name,
        };
    }

    private sealed class RecordingCollectionCatalog : ICollectionCatalog, IConfigurationCatalog
    {
        private readonly object _sync = new();
        private readonly List<CollectionPickerEntry> _entries;

        public RecordingCollectionCatalog(params CollectionPickerEntry[] entries)
        {
            _entries = [.. entries];
        }

        public Guid NextCreatedId { get; set; } = new("5dd948dd-b09c-4697-aee5-a049cd4259e7");

        public int CreateCount { get; private set; }

        public IReadOnlyList<CollectionPickerEntry> GetPickerEntries()
        {
            lock (_sync)
            {
                return [.. _entries];
            }
        }

        public Task<CollectionPickerEntry> CreateAsync(string displayName)
        {
            lock (_sync)
            {
                CreateCount++;
                var entry = new CollectionPickerEntry(NextCreatedId, displayName);
                _entries.Add(entry);
                return Task.FromResult(entry);
            }
        }

        public void Rename(Guid id, string displayName)
        {
            lock (_sync)
            {
                var index = _entries.FindIndex(entry => entry.Id == id);
                _entries[index] = new CollectionPickerEntry(id, displayName);
            }
        }

        public IReadOnlyList<Guid> GetEligibleItemIds() => [];

        public bool CollectionExists(Guid collectionId)
        {
            lock (_sync)
            {
                return _entries.Any(entry => entry.Id == collectionId);
            }
        }
    }

    private sealed class NullStateReader : IItemStateReader
    {
        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ObservedItemState?>(null);
        }
    }

    private sealed class FixedPersistence : IPluginConfigurationPersistence
    {
        public FixedPersistence(PluginConfiguration current)
        {
            Current = current;
        }

        public PluginConfiguration Current { get; }

        public void Save(PluginConfiguration configuration)
        {
            throw new InvalidOperationException("This test must not save configuration.");
        }
    }

    private sealed class ThrowingPersistence : IPluginConfigurationPersistence
    {
        public ThrowingPersistence(PluginConfiguration current)
        {
            Current = current;
        }

        public PluginConfiguration Current { get; }

        public void Save(PluginConfiguration configuration)
        {
            throw new IOException("Injected configuration save failure.");
        }
    }
}
