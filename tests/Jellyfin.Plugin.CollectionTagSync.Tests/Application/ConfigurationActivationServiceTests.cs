using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ConfigurationActivationServiceTests
{
    [Fact]
    public async Task InvalidCycleDoesNotReplaceActiveConfigurationOrEnqueueWork()
    {
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 7 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("A"), [Tag("B")], MappingPolicy.Additive),
                Group(Tag("B"), [Tag("A")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Equal(7, result.ActiveRevision);
        Assert.Null(result.ReconciliationId);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task DuplicateNormalizedTargetDoesNotReplaceActiveConfigurationOrEnqueueWork()
    {
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 7 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Target"), [Tag("A")], MappingPolicy.Additive),
                Group(Tag("target"), [Tag("B")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Equal(7, result.ActiveRevision);
        Assert.Null(result.ReconciliationId);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task StructurallyInvalidCandidateDoesNotReplaceActiveConfigurationOrEnqueueWork()
    {
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 7 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Target"), [], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Equal(7, result.ActiveRevision);
        Assert.Null(result.ReconciliationId);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task NewlySelectedMissingCollectionDoesNotSave()
    {
        var missingId = new Guid("60fb4c5d-3588-43b8-b9ee-3d5e498b83e4");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 2 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Collection(missingId, "Missing"), [Tag("Waltney")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Contains(result.ValidationErrors, error =>
            error.Code == ConfigurationActivationErrorCode.MissingCollection);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task MissingLegacyCollectionReusedInAnotherGroupDoesNotSave()
    {
        var missingId = new Guid("a1043835-cb1c-48fc-92e8-9160179824a8");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration
        {
            Revision = 2,
            MappingGroups =
            [
                Group(Tag("Existing Target"), [Collection(missingId, "Missing")], MappingPolicy.Additive),
            ],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Existing Target"), [Collection(missingId, "Missing")], MappingPolicy.Additive),
                Group(Tag("New Target"), [Collection(missingId, "Missing")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Contains(result.ValidationErrors, error =>
            error.Code == ConfigurationActivationErrorCode.MissingCollection);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task AdditionOnlyCandidatePersistsNextRevisionAndQueuesOnceBeforeReturning()
    {
        var collectionId = new Guid("482fb74b-a0ac-4281-81e0-e14117ea08cc");
        var itemId = new Guid("e73fe169-f675-4383-8e96-48e3bbe6033b");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 11 });
        var stateReader = new FixedStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []));
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], [collectionId]),
            stateReader,
            statusStore);
        var candidate = new PluginConfiguration
        {
            Revision = 999,
            MappingGroups =
            [
                Group(Collection(collectionId, "Animation"), [Tag("Waltney")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(12, result.ActiveRevision);
        Assert.Equal(12, persistence.Current.Revision);
        Assert.Equal(1, persistence.SaveCount);
        var requestId = Assert.IsType<Guid>(result.ReconciliationId);
        var status = Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId));
        Assert.Equal(BackgroundReconciliationState.Queued, status.State);
        Assert.Equal(12, status.ConfigurationRevision);
        Assert.Equal(1, status.TotalItemCount);
        Assert.Equal(1, statusStore.Count);
    }

    [Fact]
    public async Task RemovalBearingCandidateIsPausedWithoutSaving()
    {
        var itemId = new Guid("b0f3553b-d182-4427-896c-580322bbd9e0");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 3 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new FixedStateReader(new ObservedItemState(
                itemId,
                EligibleItemKind.Series,
                directTags: ["Kid-Approved"],
                directCollectionIds: [])),
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(
                    Tag("Kid-Approved"),
                    [Tag("Absent")],
                    MappingPolicy.Authoritative),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.RequiresPreview, result.Outcome);
        Assert.Equal(3, result.ActiveRevision);
        Assert.Equal(0, persistence.SaveCount);
        var requestId = Assert.IsType<Guid>(result.ReconciliationId);
        Assert.Equal(
            BackgroundReconciliationState.Paused,
            Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId)).State);
    }

    [Fact]
    public async Task ImpactEvaluationAndPersistenceWaitForTheSharedExecutionGate()
    {
        var itemId = new Guid("c9f5efea-471d-43bd-9392-dc487342dfeb");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 4 });
        var stateReader = new FixedStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: [],
            directCollectionIds: []));
        var statusStore = new BackgroundReconciliationStatusStore();
        var executionGate = new ReconciliationExecutionGate();
        await executionGate.EnterAsync(CancellationToken.None).ConfigureAwait(true);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            stateReader,
            statusStore,
            executionGate);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Target"), [Tag("Source")], MappingPolicy.Additive),
            ],
        };

        var activation = service.ActivateAsync(candidate, CancellationToken.None);
        await Task.Yield();

        Assert.False(activation.IsCompleted);
        Assert.Equal(0, stateReader.ReadCount);
        Assert.Equal(0, persistence.SaveCount);

        executionGate.Exit();
        var result = await activation.ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(1, stateReader.ReadCount);
        Assert.Equal(1, persistence.SaveCount);
    }

    [Fact]
    public async Task LaterActivationReturnsBeforePriorRequestFinishesSettling()
    {
        var firstItemId = new Guid("48a27ed7-e1e7-4a46-ac5e-6f0826fdd563");
        var secondItemId = new Guid("4521dd71-31d0-455f-b2f4-d0f0cc36df61");
        var firstConfiguration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Target"),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var executionGate = new ReconciliationExecutionGate();
        var firstRequestId = dispatcher.Enqueue(
            revision: 5,
            [firstItemId, secondItemId],
            firstConfiguration);
        var stateReader = new SequencedBlockingStateReader();
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(
                new FixedMappingProvider(firstConfiguration),
                stateReader,
                new RejectingWriter()),
            new PassThroughOperationalMappingProvider(),
            executionGate,
            new RecordingFailureQuarantine(),
            NullLogger<ConfigurationReconciliationWorker>.Instance);
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 5 });
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            statusStore,
            executionGate,
            dispatcher);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await stateReader.FirstStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        var activation = service.ActivateAsync(
            new PluginConfiguration { MappingGroups = [] },
            CancellationToken.None);
        await Task.Yield();
        stateReader.ReleaseFirst();

        var result = await activation.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(BackgroundReconciliationState.Running, statusStore.Get(firstRequestId)?.State);
        await stateReader.SecondStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        stateReader.ReleaseSecond();
        var secondRequestId = Assert.IsType<Guid>(result.ReconciliationId);
        await WaitUntilAsync(() =>
            statusStore.Get(firstRequestId)?.State == BackgroundReconciliationState.Completed
            && statusStore.Get(secondRequestId)?.State == BackgroundReconciliationState.Completed)
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private static ConfigurationActivationService CreateService(
        RecordingConfigurationPersistence persistence,
        FixedCatalog catalog,
        FixedStateReader stateReader,
        BackgroundReconciliationStatusStore statusStore,
        ReconciliationExecutionGate? executionGate = null,
        ConfigurationReconciliationDispatcher? dispatcher = null)
    {
        return new ConfigurationActivationService(
            persistence,
            catalog,
            stateReader,
            dispatcher ?? new ConfigurationReconciliationDispatcher(statusStore),
            statusStore,
            executionGate ?? new ReconciliationExecutionGate());
    }

    private static MappingGroupConfiguration Group(
        MappingNodeConfiguration target,
        MappingNodeConfiguration[] sources,
        MappingPolicy policy)
    {
        return new MappingGroupConfiguration
        {
            Target = target,
            Sources = sources,
            Policy = policy,
            IsEnabled = true,
        };
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

    private sealed class RecordingConfigurationPersistence : IPluginConfigurationPersistence
    {
        public RecordingConfigurationPersistence(PluginConfiguration current)
        {
            Current = current;
        }

        public PluginConfiguration Current { get; private set; }

        public int SaveCount { get; private set; }

        public void Save(PluginConfiguration configuration)
        {
            Current = configuration;
            SaveCount++;
        }
    }

    private sealed class FixedCatalog : IConfigurationCatalog
    {
        private readonly IReadOnlyList<Guid> _eligibleItemIds;
        private readonly HashSet<Guid> _collectionIds;

        public FixedCatalog(IEnumerable<Guid> eligibleItemIds, IEnumerable<Guid> collectionIds)
        {
            _eligibleItemIds = [.. eligibleItemIds];
            _collectionIds = [.. collectionIds];
        }

        public IReadOnlyList<Guid> GetEligibleItemIds()
        {
            return _eligibleItemIds;
        }

        public bool CollectionExists(Guid collectionId)
        {
            return _collectionIds.Contains(collectionId);
        }
    }

    private sealed class FixedStateReader : IItemStateReader
    {
        private readonly Dictionary<Guid, ObservedItemState> _states;

        public FixedStateReader(params ObservedItemState[] states)
        {
            _states = new Dictionary<Guid, ObservedItemState>();
            foreach (var state in states)
            {
                _states[state.ItemId] = state;
            }
        }

        public int ReadCount { get; private set; }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(_states.GetValueOrDefault(itemId));
        }
    }

    private sealed class FixedMappingProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration _configuration;

        public FixedMappingProvider(MappingConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MappingConfiguration? GetConfiguration()
        {
            return _configuration;
        }
    }

    private sealed class PassThroughOperationalMappingProvider : IOperationalMappingProvider
    {
        public MappingConfiguration Resolve(MappingConfiguration configuration)
        {
            return configuration;
        }
    }

    private sealed class SequencedBlockingStateReader : IItemStateReader
    {
        private readonly TaskCompletionSource _firstStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecond = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public Task FirstStarted => _firstStarted.Task;

        public Task SecondStarted => _secondStarted.Task;

        public void ReleaseFirst()
        {
            _releaseFirst.SetResult();
        }

        public void ReleaseSecond()
        {
            _releaseSecond.SetResult();
        }

        public async Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _readCount++;
            if (_readCount == 1)
            {
                _firstStarted.SetResult();
                await _releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _secondStarted.SetResult();
                await _releaseSecond.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                directTags: [],
                directCollectionIds: []);
        }
    }

    private sealed class RecordingFailureQuarantine : IFailedItemQuarantine
    {
        public void Quarantine(Guid itemId)
        {
            throw new InvalidOperationException("A settled request must not quarantine items.");
        }
    }

    private sealed class RejectingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A settled request must not write mutations.");
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(true);
        }
    }
}
