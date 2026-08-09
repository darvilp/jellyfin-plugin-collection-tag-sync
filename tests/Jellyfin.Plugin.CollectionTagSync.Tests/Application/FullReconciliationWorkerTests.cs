using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class FullReconciliationWorkerTests
{
    [Fact]
    public async Task ExcessiveDestructivePlanPausesBeforeEveryWriteAndPersistsOnlyPreviewDiagnostics()
    {
        var itemIds = Enumerable.Range(0, 26)
            .Select(index => Guid.Parse($"10000000-0000-0000-0000-{index + 1:D12}"))
            .ToArray();
        var configuration = CreateConfiguration();
        var reader = new MutableStateReader(itemIds.Select(itemId => new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            ["Target", "Second Target"],
            [])).ToArray());
        var writer = new RecordingWriter(reader, Guid.Empty, expectedReadsBeforeFirstWrite: itemIds.Length)
        {
            FailedItemId = null,
        };
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 4 });
        var statusStore = new FullReconcileStatusStore();
        var requestStore = new FullReconcileRequestStore();
        using var worker = new FullReconciliationWorker(
            requestStore,
            statusStore,
            new FixedCatalog(itemIds),
            new FixedConfigurationProvider(configuration),
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new ReconciliationExecutionGate(),
            new RecordingIncrementalRecovery(),
            new AlwaysQuietActivityMonitor(),
            new FullReconcileSafetyService(persistence, TimeProvider.System),
            NullLogger<FullReconciliationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);

        var result = await requestStore
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(FullReconcileState.AwaitingApproval, result.State);
        Assert.Equal(0, writer.ApplyCount);
        var preview = Assert.IsType<PausedFullReconcileConfiguration>(
            persistence.Current.PausedFullReconcile);
        Assert.Equal(result.Id, preview.RunId);
        Assert.Equal(4, preview.ConfigurationRevision);
        Assert.Equal(26, preview.UniqueAffectedItemCount);
        Assert.Equal(52, preview.Removals.Length);
        Assert.Equal(2, preview.Groups.Length);
        Assert.Equal(26, preview.Items.Length);
    }

    [Fact]
    public async Task MatchingConfirmationRecomputesThenExecutesTheFreshPlan()
    {
        var itemIds = Enumerable.Range(0, 26)
            .Select(index => Guid.Parse($"20000000-0000-0000-0000-{index + 1:D12}"))
            .ToArray();
        var configuration = CreateConfiguration();
        var reader = new MutableStateReader(itemIds.Select(itemId => new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            ["Target", "Second Target"],
            [])).ToArray());
        var writer = new RecordingWriter(reader, Guid.Empty, expectedReadsBeforeFirstWrite: itemIds.Length)
        {
            FailedItemId = null,
        };
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 8 });
        var requests = new FullReconcileRequestStore();
        var safety = new FullReconcileSafetyService(persistence, TimeProvider.System);
        var approval = new FullReconcileApprovalService(requests, safety);
        using var worker = new FullReconciliationWorker(
            requests,
            new FullReconcileStatusStore(),
            new FixedCatalog(itemIds),
            new FixedConfigurationProvider(configuration),
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new ReconciliationExecutionGate(),
            new RecordingIncrementalRecovery(),
            new AlwaysQuietActivityMonitor(),
            safety,
            NullLogger<FullReconciliationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        var paused = await requests
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        var administratorId = new Guid("911a2579-e150-407c-a919-b5af378f34b5");
        var authorization = Assert.IsType<FullReconcilePreviewAuthorization>(
            safety.CreatePreviewAuthorization(paused.Id, administratorId));

        var confirmed = await approval
            .ConfirmAsync(
                paused.Id,
                administratorId,
                authorization.Authorization,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(FullReconcileConfirmationOutcome.Accepted, confirmed.Outcome);
        Assert.Equal(FullReconcileState.Completed, confirmed.RunResult?.State);
        Assert.Equal(26, writer.ApplyCount);
        Assert.Null(persistence.Current.PausedFullReconcile);
    }

    [Fact]
    public async Task ChangedRemovalSetRejectsConfirmationWithoutWritesAndRequiresNewPreview()
    {
        var itemIds = Enumerable.Range(0, 26)
            .Select(index => Guid.Parse($"30000000-0000-0000-0000-{index + 1:D12}"))
            .ToArray();
        var configuration = CreateConfiguration();
        var reader = new MutableStateReader(itemIds.Select(itemId => new ObservedItemState(
            itemId,
            EligibleItemKind.Series,
            ["Target", "Second Target"],
            [])).ToArray());
        var writer = new RecordingWriter(reader, Guid.Empty, expectedReadsBeforeFirstWrite: itemIds.Length)
        {
            FailedItemId = null,
        };
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 12 });
        var requests = new FullReconcileRequestStore();
        var safety = new FullReconcileSafetyService(persistence, TimeProvider.System);
        var approval = new FullReconcileApprovalService(requests, safety);
        using var worker = new FullReconciliationWorker(
            requests,
            new FullReconcileStatusStore(),
            new FixedCatalog(itemIds),
            new FixedConfigurationProvider(configuration),
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new ReconciliationExecutionGate(),
            new RecordingIncrementalRecovery(),
            new AlwaysQuietActivityMonitor(),
            safety,
            NullLogger<FullReconciliationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        var paused = await requests
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        var administratorId = new Guid("43ed632d-0a38-42ec-8d00-240846cb1b7e");
        var authorization = Assert.IsType<FullReconcilePreviewAuthorization>(
            safety.CreatePreviewAuthorization(paused.Id, administratorId));
        reader.RemoveTag(itemIds[0], "Target");

        var confirmed = await approval
            .ConfirmAsync(
                paused.Id,
                administratorId,
                authorization.Authorization,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        var reused = await approval
            .ConfirmAsync(
                paused.Id,
                administratorId,
                authorization.Authorization,
                CancellationToken.None)
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(FullReconcileConfirmationOutcome.StalePreview, confirmed.Outcome);
        Assert.Equal(FullReconcileState.AwaitingApproval, confirmed.RunResult?.State);
        Assert.Equal(FullReconcileConfirmationOutcome.InvalidAuthorization, reused.Outcome);
        Assert.Equal(0, writer.ApplyCount);
        var replacement = Assert.IsType<PausedFullReconcileConfiguration>(
            persistence.Current.PausedFullReconcile);
        Assert.NotEqual(paused.Id, replacement.RunId);
        Assert.Equal(51, replacement.Removals.Length);
    }

    [Fact]
    public async Task FullReconcilePlansEveryItemBeforeWritingContinuesAfterFailureAndReleasesRepairs()
    {
        var missedAdditionId = new Guid("83d97c1a-b505-4f4a-a884-a142a49446d9");
        var unsupportedTargetId = new Guid("57bef9dd-d42e-4946-8f81-c99b1c203c97");
        var partialWriteId = new Guid("2c2410fe-af8f-44ab-b6a8-87f09538bcc1");
        var failedItemId = new Guid("ae6f8ae0-9aaf-4144-a6b9-0b6298baaf49");
        var laterItemId = new Guid("1f0071c0-0764-415c-8504-dac5d86b6f7a");
        var itemIds = new[]
        {
            missedAdditionId,
            unsupportedTargetId,
            partialWriteId,
            failedItemId,
            laterItemId,
        };
        var configuration = CreateConfiguration();
        var reader = new MutableStateReader(
            new ObservedItemState(missedAdditionId, EligibleItemKind.Movie, ["Source"], []),
            new ObservedItemState(unsupportedTargetId, EligibleItemKind.Series, ["Target", "Second Target"], []),
            new ObservedItemState(partialWriteId, EligibleItemKind.Movie, ["Source", "Target"], []),
            new ObservedItemState(failedItemId, EligibleItemKind.Movie, ["Source"], []),
            new ObservedItemState(laterItemId, EligibleItemKind.Series, ["Source"], []));
        var writer = new RecordingWriter(reader, failedItemId, expectedReadsBeforeFirstWrite: itemIds.Length);
        var statusStore = new FullReconcileStatusStore();
        var requestStore = new FullReconcileRequestStore();
        var recovery = new RecordingIncrementalRecovery();
        using var worker = new FullReconciliationWorker(
            requestStore,
            statusStore,
            new FixedCatalog(itemIds),
            new FixedConfigurationProvider(configuration),
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new ReconciliationExecutionGate(),
            recovery,
            new AlwaysQuietActivityMonitor(),
            CreateSafety(),
            NullLogger<FullReconciliationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);

        var firstResult = await requestStore
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);

        Assert.Equal(FullReconcileState.CompletedWithFailures, firstResult.State);
        Assert.Equal(5, firstResult.TotalItemCount);
        Assert.Equal(4, firstResult.SucceededItemCount);
        Assert.Equal(1, firstResult.FailedItemCount);
        Assert.Contains("Target", reader.GetTags(missedAdditionId));
        Assert.Contains("Second Target", reader.GetTags(missedAdditionId));
        Assert.DoesNotContain("Target", reader.GetTags(unsupportedTargetId));
        Assert.DoesNotContain("Second Target", reader.GetTags(unsupportedTargetId));
        Assert.Contains("Target", reader.GetTags(partialWriteId));
        Assert.Contains("Second Target", reader.GetTags(partialWriteId));
        Assert.DoesNotContain("Target", reader.GetTags(failedItemId));
        Assert.Contains("Target", reader.GetTags(laterItemId));
        Assert.Equal(
            new[] { missedAdditionId, unsupportedTargetId, partialWriteId, laterItemId }.Order(),
            recovery.LastRepairedItemIds.Order());
        Assert.Equal(FullReconcileState.CompletedWithFailures, statusStore.Current.State);

        writer.FailedItemId = null;
        var secondResult = await requestStore
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(FullReconcileState.Completed, secondResult.State);
        Assert.Equal(5, secondResult.SucceededItemCount);
        Assert.Equal(0, secondResult.FailedItemCount);
        Assert.Contains("Target", reader.GetTags(failedItemId));
        Assert.Contains("Second Target", reader.GetTags(failedItemId));
        Assert.Contains(failedItemId, recovery.LastRepairedItemIds);
        Assert.Equal(2, recovery.CompletionCount);
    }

    [Fact]
    public async Task PendingReasonsCoalesceWhileRequestArrivingDuringRunCreatesOneFollowUp()
    {
        var store = new FullReconcileRequestStore();
        var startup = store.RequestAsync(FullReconcileRequestReason.Startup, CancellationToken.None);
        var manual = store.RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None);

        Assert.True(store.TryClaim(out var first));
        Assert.Equal(
            new[] { FullReconcileRequestReason.Startup, FullReconcileRequestReason.Manual }.Order(),
            first.Reasons.Order());

        var storm = store.RequestAsync(FullReconcileRequestReason.EventStorm, CancellationToken.None);
        var repeatedStorm = store.RequestAsync(FullReconcileRequestReason.EventStorm, CancellationToken.None);
        Assert.True(store.TryClaim(out var second));
        Assert.Equal([FullReconcileRequestReason.EventStorm], second.Reasons);

        first.Complete(Completed(first));
        second.Complete(Completed(second));

        Assert.Equal(first.Id, (await startup.ConfigureAwait(true)).Id);
        Assert.Equal(first.Id, (await manual.ConfigureAwait(true)).Id);
        Assert.Equal(second.Id, (await storm.ConfigureAwait(true)).Id);
        Assert.Equal(second.Id, (await repeatedStorm.ConfigureAwait(true)).Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task StartupAndStormRequestsRemainCoalescedWhileWaitingForQuiet()
    {
        var requests = new FullReconcileRequestStore();
        var statusStore = new FullReconcileStatusStore();
        var recovery = new RecordingIncrementalRecovery();
        var activity = new BlockingActivityMonitor();
        using var worker = new FullReconciliationWorker(
            requests,
            statusStore,
            new FixedCatalog([]),
            new NullConfigurationProvider(),
            new ItemReconciler(
                new NullConfigurationProvider(),
                new MutableStateReader(),
                new RecordingWriter(new MutableStateReader(), Guid.Empty, 1)),
            new ReconciliationExecutionGate(),
            recovery,
            activity,
            CreateSafety(),
            NullLogger<FullReconciliationWorker>.Instance);
        var startup = requests.RequestAsync(FullReconcileRequestReason.Startup, CancellationToken.None);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await activity.WaitStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

        var storm = requests.RequestAsync(FullReconcileRequestReason.EventStorm, CancellationToken.None);
        activity.Release();
        var startupResult = await startup.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        var stormResult = await storm.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(startupResult.Id, stormResult.Id);
        Assert.Equal(
            new[] { FullReconcileRequestReason.Startup, FullReconcileRequestReason.EventStorm }.Order(),
            startupResult.Reasons.Order());
        Assert.Equal(1, activity.WaitCount);
    }

    [Fact]
    public async Task SuccessfulRecoveryActuallyReleasesTheRepairedItemFromIncrementalQuarantine()
    {
        var itemId = new Guid("08f62b6f-d921-4c8b-a4b9-b7a77752265d");
        var configuration = CreateConfiguration();
        var reader = new MutableStateReader(
            new ObservedItemState(itemId, EligibleItemKind.Movie, ["Source"], []));
        var writer = new RecordingWriter(reader, Guid.Empty, expectedReadsBeforeFirstWrite: 1)
        {
            FailedItemId = null,
        };
        var requests = new FullReconcileRequestStore();
        using var incremental = new ReconciliationWorker(
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new IncrementalReconciliationOptions(),
            requests,
            NullLogger<ReconciliationWorker>.Instance);
        ((IFailedItemQuarantine)incremental).Quarantine(itemId);
        Assert.Equal(1, incremental.Status.QuarantinedItemCount);
        using var full = new FullReconciliationWorker(
            requests,
            new FullReconcileStatusStore(),
            new FixedCatalog([itemId]),
            new FixedConfigurationProvider(configuration),
            new ItemReconciler(new FixedConfigurationProvider(configuration), reader, writer),
            new ReconciliationExecutionGate(),
            incremental,
            new AlwaysQuietActivityMonitor(),
            CreateSafety(),
            NullLogger<FullReconciliationWorker>.Instance);
        await full.StartAsync(CancellationToken.None).ConfigureAwait(true);

        var result = await requests
            .RequestAsync(FullReconcileRequestReason.Manual, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(true);
        await full.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(FullReconcileState.Completed, result.State);
        Assert.Equal(0, incremental.Status.QuarantinedItemCount);
    }

    [Fact]
    public async Task EventStormRequestIsConsumedAndReleasesIncrementalStormFallback()
    {
        var requests = new FullReconcileRequestStore();
        var nullProvider = new NullConfigurationProvider();
        var reader = new MutableStateReader();
        using var incremental = new ReconciliationWorker(
            new ItemReconciler(nullProvider, reader, new RejectingWriter()),
            new IncrementalReconciliationOptions(maxPendingItems: 1),
            requests,
            NullLogger<ReconciliationWorker>.Instance);
        incremental.MarkDirty(new Guid("a6de86ee-06b5-4034-89cb-5f758239d762"));
        incremental.MarkDirty(new Guid("64a013f1-90db-4b88-a87e-1b48781eb5de"));
        Assert.True(incremental.Status.IsStormFallbackActive);
        Assert.Contains(FullReconcileRequestReason.EventStorm, requests.Status.Reasons);
        var statusStore = new FullReconcileStatusStore();
        using var full = new FullReconciliationWorker(
            requests,
            statusStore,
            new FixedCatalog([]),
            nullProvider,
            new ItemReconciler(nullProvider, reader, new RejectingWriter()),
            new ReconciliationExecutionGate(),
            incremental,
            new AlwaysQuietActivityMonitor(),
            CreateSafety(),
            NullLogger<FullReconciliationWorker>.Instance);

        await full.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() => statusStore.Current.State == FullReconcileState.Completed)
            .ConfigureAwait(true);
        await full.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.False(incremental.Status.IsStormFallbackActive);
        Assert.False(requests.Status.IsRequested);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(true);
        }
    }

    private static MappingConfiguration CreateConfiguration()
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("Target"),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new TagNodeDefinition("Second Target"),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
            ]).Configuration);
    }

    private static FullReconcileSafetyService CreateSafety()
    {
        return new FullReconcileSafetyService(
            new RecordingConfigurationPersistence(new PluginConfiguration()),
            TimeProvider.System);
    }

    private static FullReconcileRunResult Completed(FullReconcileRequest request)
    {
        return new FullReconcileRunResult(
            request.Id,
            FullReconcileState.Completed,
            request.Reasons,
            totalItemCount: 0,
            succeededItemCount: 0,
            failedItemCount: 0);
    }

    private sealed class FixedCatalog : IConfigurationCatalog
    {
        private readonly IReadOnlyList<Guid> _itemIds;

        public FixedCatalog(IReadOnlyList<Guid> itemIds)
        {
            _itemIds = itemIds;
        }

        public IReadOnlyList<Guid> GetEligibleItemIds()
        {
            return _itemIds;
        }

        public bool CollectionExists(Guid collectionId)
        {
            return false;
        }
    }

    private sealed class FixedConfigurationProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration _configuration;

        public FixedConfigurationProvider(MappingConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MappingConfiguration? GetConfiguration()
        {
            return _configuration;
        }
    }

    private sealed class NullConfigurationProvider : IActiveMappingProvider
    {
        public MappingConfiguration? GetConfiguration()
        {
            return null;
        }
    }

    private sealed class MutableStateReader : IItemStateReader
    {
        private readonly Dictionary<Guid, ObservedItemState> _states;

        public MutableStateReader(params ObservedItemState[] states)
        {
            _states = states.ToDictionary(state => state.ItemId);
        }

        public int ReadCount { get; private set; }

        public IReadOnlyList<string> GetTags(Guid itemId)
        {
            return _states[itemId].DirectTags;
        }

        public void AddTag(Guid itemId, string tag)
        {
            var state = _states[itemId];
            _states[itemId] = new ObservedItemState(
                itemId,
                state.ItemKind,
                state.DirectTags.Append(tag),
                state.DirectCollectionIds);
        }

        public void RemoveTag(Guid itemId, string tag)
        {
            var state = _states[itemId];
            _states[itemId] = new ObservedItemState(
                itemId,
                state.ItemKind,
                state.DirectTags.Where(value => !string.Equals(value, tag, StringComparison.Ordinal)),
                state.DirectCollectionIds);
        }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<ObservedItemState?>(_states[itemId]);
        }
    }

    private sealed class RecordingWriter : IPlanWriter
    {
        private readonly int _expectedReadsBeforeFirstWrite;
        private readonly MutableStateReader _reader;

        public RecordingWriter(
            MutableStateReader reader,
            Guid failedItemId,
            int expectedReadsBeforeFirstWrite)
        {
            _reader = reader;
            FailedItemId = failedItemId;
            _expectedReadsBeforeFirstWrite = expectedReadsBeforeFirstWrite;
        }

        public Guid? FailedItemId { get; set; }

        public int ApplyCount { get; private set; }

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            ApplyCount++;
            Assert.Equal(0, _reader.ReadCount % _expectedReadsBeforeFirstWrite);
            if (plan.ItemId == FailedItemId)
            {
                throw new InvalidOperationException("Injected Full Reconcile item failure.");
            }

            foreach (var mutation in plan.Mutations)
            {
                var tag = Assert.IsType<TagNode>(mutation.Target).Value;
                if (mutation.Kind == PlannedMutationKind.AddTag)
                {
                    _reader.AddTag(plan.ItemId, tag);
                }
                else if (mutation.Kind == PlannedMutationKind.RemoveTag)
                {
                    _reader.RemoveTag(plan.ItemId, tag);
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingConfigurationPersistence : IPluginConfigurationPersistence
    {
        public RecordingConfigurationPersistence(PluginConfiguration current)
        {
            Current = current;
        }

        public PluginConfiguration Current { get; private set; }

        public void Save(PluginConfiguration configuration)
        {
            Current = configuration;
        }
    }

    private sealed class RejectingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A null active configuration must never write a plan.");
        }
    }

    private sealed class RecordingIncrementalRecovery : IIncrementalReconciliationControl
    {
        public IncrementalReconciliationStatus Status { get; } =
            new(queuedItemCount: 0, runningItemCount: 0, quarantinedItemCount: 0, isStormFallbackActive: false);

        public int CompletionCount { get; private set; }

        public IReadOnlyList<Guid> LastRepairedItemIds { get; private set; } = [];

        public void CompleteFullReconcile(
            IEnumerable<Guid> repairedItemIds,
            IEnumerable<Guid> failedItemIds)
        {
            LastRepairedItemIds = [.. repairedItemIds];
            CompletionCount++;
        }
    }

    private sealed class AlwaysQuietActivityMonitor : IReconciliationActivityMonitor
    {
        public void RecordActivity()
        {
        }

        public Task WaitUntilQuietAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingActivityMonitor : IReconciliationActivityMonitor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitStarted => _waitStarted.Task;

        public int WaitCount { get; private set; }

        public void RecordActivity()
        {
        }

        public Task WaitUntilQuietAsync(CancellationToken cancellationToken)
        {
            WaitCount++;
            _waitStarted.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }

        public void Release()
        {
            _release.TrySetResult();
        }
    }
}
