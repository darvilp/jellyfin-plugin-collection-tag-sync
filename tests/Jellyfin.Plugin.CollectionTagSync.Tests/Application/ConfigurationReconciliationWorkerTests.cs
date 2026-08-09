using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ConfigurationReconciliationWorkerTests
{
    [Fact]
    public void MixedItemOutcomesReportPartialFailure()
    {
        var statusStore = new BackgroundReconciliationStatusStore();
        var requestId = statusStore.CreateQueued(revision: 3, totalItemCount: 2);

        statusStore.MarkRunning(requestId);
        statusStore.RecordSuccess(requestId);
        statusStore.RecordFailure(requestId);
        statusStore.MarkFinished(requestId);

        var status = Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId));
        Assert.Equal(BackgroundReconciliationState.PartiallyFailed, status.State);
        Assert.Equal(1, status.CompletedItemCount);
        Assert.Equal(1, status.FailedItemCount);
    }

    [Fact]
    public async Task StatusMovesFromQueuedThroughRunningToCompleted()
    {
        var itemId = new Guid("69ba1083-e063-47cb-81f4-4798e4c47365");
        var stateReader = new BlockingStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: [],
            directCollectionIds: []));
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var requestId = dispatcher.Enqueue(revision: 4, [itemId], CreateConfiguration("Target"));
        var quarantine = new RecordingFailureQuarantine();
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(new FixedConfigurationProvider(), stateReader, new RejectingWriter()),
            new PassThroughOperationalMappingProvider(),
            new ReconciliationExecutionGate(),
            quarantine,
            NullLogger<ConfigurationReconciliationWorker>.Instance);

        Assert.Equal(BackgroundReconciliationState.Queued, statusStore.Get(requestId)?.State);
        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await stateReader.Started.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

        Assert.Equal(BackgroundReconciliationState.Running, statusStore.Get(requestId)?.State);
        stateReader.Release();
        await WaitUntilAsync(() =>
            statusStore.Get(requestId)?.State == BackgroundReconciliationState.Completed).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        var completed = Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId));
        Assert.Equal(1, completed.CompletedItemCount);
        Assert.Equal(0, completed.FailedItemCount);
        Assert.Empty(quarantine.ItemIds);
    }

    [Fact]
    public async Task RequestRemainsQueuedWhileAnotherExecutionOwnsTheGate()
    {
        var itemId = new Guid("6c26f6e0-b8cb-420a-b81a-35caf9d0b0b4");
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var requestId = dispatcher.Enqueue(revision: 5, [itemId], CreateConfiguration("Target"));
        var executionGate = new ReconciliationExecutionGate();
        await executionGate.EnterAsync(CancellationToken.None).ConfigureAwait(true);
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(
                new FixedConfigurationProvider(),
                new FixedStateReader(new ObservedItemState(
                    itemId,
                    EligibleItemKind.Movie,
                    directTags: [],
                    directCollectionIds: [])),
                new RejectingWriter()),
            new PassThroughOperationalMappingProvider(),
            executionGate,
            new RecordingFailureQuarantine(),
            NullLogger<ConfigurationReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await Task.Delay(100).ConfigureAwait(true);

        Assert.Equal(BackgroundReconciliationState.Queued, statusStore.Get(requestId)?.State);

        executionGate.Exit();
        await WaitUntilAsync(() =>
            statusStore.Get(requestId)?.State == BackgroundReconciliationState.Completed).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [Fact]
    public async Task FailedRequestReportsFailureAndQuarantinesItem()
    {
        var itemId = new Guid("8b808e80-8b91-44f0-8459-8ddab4117733");
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var requestId = dispatcher.Enqueue(revision: 8, [itemId], CreateConfiguration("Target"));
        var quarantine = new RecordingFailureQuarantine();
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(
                new FixedConfigurationProvider(),
                new FixedStateReader(new ObservedItemState(
                    itemId,
                    EligibleItemKind.Series,
                    directTags: ["Source"],
                    directCollectionIds: [])),
                new ThrowingWriter()),
            new PassThroughOperationalMappingProvider(),
            new ReconciliationExecutionGate(),
            quarantine,
            NullLogger<ConfigurationReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() =>
            statusStore.Get(requestId)?.State == BackgroundReconciliationState.Failed).ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        var failed = Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId));
        Assert.Equal(0, failed.CompletedItemCount);
        Assert.Equal(1, failed.FailedItemCount);
        Assert.Equal(itemId, Assert.Single(quarantine.ItemIds));
    }

    [Fact]
    public async Task EachQueuedRevisionReconcilesItsAcceptedConfigurationSnapshot()
    {
        var itemId = new Guid("6e746290-f21c-455e-87ec-f3dc0e284876");
        var firstConfiguration = CreateConfiguration("First Target");
        var secondConfiguration = CreateConfiguration("Second Target");
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var firstRequestId = dispatcher.Enqueue(revision: 10, [itemId], firstConfiguration);
        var secondRequestId = dispatcher.Enqueue(revision: 11, [itemId], secondConfiguration);
        var state = new MutableTagState(itemId, "Source");
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(new FixedConfigurationProvider(secondConfiguration), state, state),
            new PassThroughOperationalMappingProvider(),
            new ReconciliationExecutionGate(),
            new RecordingFailureQuarantine(),
            NullLogger<ConfigurationReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        await WaitUntilAsync(() =>
            statusStore.Get(firstRequestId)?.State == BackgroundReconciliationState.Completed
            && statusStore.Get(secondRequestId)?.State == BackgroundReconciliationState.Completed)
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(["First Target", "Second Target"], state.AddedTargets);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(true);
        }
    }

    private sealed class FixedConfigurationProvider : IActiveMappingProvider
    {
        private readonly MappingConfiguration _configuration;

        public FixedConfigurationProvider()
            : this(CreateConfiguration("Target"))
        {
        }

        public FixedConfigurationProvider(MappingConfiguration configuration)
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

    private sealed class MutableTagState : IItemStateReader, IPlanWriter
    {
        private readonly Guid _itemId;
        private readonly HashSet<string> _tags;

        public MutableTagState(Guid itemId, params string[] tags)
        {
            _itemId = itemId;
            _tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        }

        public List<string> AddedTargets { get; } = [];

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ObservedItemState?>(new ObservedItemState(
                _itemId,
                EligibleItemKind.Movie,
                _tags,
                directCollectionIds: []));
        }

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            var mutation = Assert.Single(plan.Mutations);
            Assert.Equal(PlannedMutationKind.AddTag, mutation.Kind);
            var value = Assert.Single(mutation.TagValues);
            _tags.Add(value);
            AddedTargets.Add(Assert.IsType<TagNode>(mutation.Target).Value);
            return Task.CompletedTask;
        }
    }

    private static MappingConfiguration CreateConfiguration(string target)
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition(target),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
    }

    private sealed class BlockingStateReader : IItemStateReader
    {
        private readonly ObservedItemState _state;
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingStateReader(ObservedItemState state)
        {
            _state = state;
        }

        public Task Started => _started.Task;

        public void Release()
        {
            _release.SetResult();
        }

        public async Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _started.SetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return _state;
        }
    }

    private sealed class FixedStateReader : IItemStateReader
    {
        private readonly ObservedItemState _state;

        public FixedStateReader(ObservedItemState state)
        {
            _state = state;
        }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ObservedItemState?>(_state);
        }
    }

    private sealed class RecordingFailureQuarantine : IFailedItemQuarantine
    {
        public List<Guid> ItemIds { get; } = [];

        public void Quarantine(Guid itemId)
        {
            ItemIds.Add(itemId);
        }
    }

    private sealed class RejectingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A settled plan must not invoke the writer.");
        }
    }

    private sealed class ThrowingWriter : IPlanWriter
    {
        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Injected configuration reconciliation failure.");
        }
    }
}
