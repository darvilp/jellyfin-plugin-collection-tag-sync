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
            StartupReconcileDelayMinutes = 60,
            MappingGroups =
            [
                Group(Collection(collectionId, "Animation"), [Tag("Waltney")], MappingPolicy.Additive),
            ],
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(12, result.ActiveRevision);
        Assert.Equal(12, persistence.Current.Revision);
        Assert.Equal(60, persistence.Current.StartupReconcileDelayMinutes);
        Assert.Equal(1, persistence.SaveCount);
        var requestId = Assert.IsType<Guid>(result.ReconciliationId);
        var status = Assert.IsType<BackgroundReconciliationStatus>(statusStore.Get(requestId));
        Assert.Equal(BackgroundReconciliationState.Queued, status.State);
        Assert.Equal(12, status.ConfigurationRevision);
        Assert.Equal(1, status.TotalItemCount);
        Assert.Equal(1, statusStore.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task InvalidStartupReconcileDelayDoesNotReplaceActiveConfiguration(int delayMinutes)
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
            StartupReconcileDelayMinutes = delayMinutes,
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Equal(7, result.ActiveRevision);
        Assert.Contains(result.ValidationErrors, error =>
            error.Code == ConfigurationActivationErrorCode.InvalidCandidate);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
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
    public async Task PreviewReturnsCompleteSettledItemPlanWithoutSavingCandidate()
    {
        var itemId = new Guid("4feff41d-090b-43e8-bc98-578e886f230b");
        var administratorId = new Guid("5d162f8d-4a72-47df-9ad2-2a3653ce91ea");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 3 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new FixedStateReader(new ObservedItemState(
                itemId,
                EligibleItemKind.Series,
                directTags: ["Parent", "Child", "Source"],
                directCollectionIds: [])),
            statusStore,
            itemTitleProvider: new TestItemTitleProvider((itemId, "Waltney Adventure")),
            timeProvider: time);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Parent"), [Tag("Absent")], MappingPolicy.Authoritative),
                Group(Tag("Child"), [Tag("Parent")], MappingPolicy.Authoritative),
                Group(Tag("Added"), [Tag("Source")], MappingPolicy.Additive),
            ],
        };

        var result = await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationPreviewOutcome.Ready, result.Outcome);
        Assert.Equal(3, result.ActiveRevision);
        Assert.Empty(result.ValidationErrors);
        var authorization = Assert.IsType<ConfigurationPreviewAuthorization>(result.Authorization);
        Assert.False(string.IsNullOrWhiteSpace(authorization.Authorization));
        Assert.Equal(time.GetUtcNow().AddMinutes(10), authorization.ExpiresAtUtc);
        Assert.Equal(3, authorization.Preview.ActiveConfigurationRevision);
        Assert.Equal(1, authorization.Preview.TotalItemCount);
        var item = Assert.Single(authorization.Preview.Items);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal("Waltney Adventure", item.ItemTitle);
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.RemoveTag
            && mutation.Target.TagValue == "Parent");
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.RemoveTag
            && mutation.Target.TagValue == "Child");
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.TagValue == "Added");
        Assert.Contains(item.TargetEvaluations, evaluation =>
            evaluation.Target.TagValue == "Parent"
            && evaluation.ObservedState
            && !evaluation.EffectiveState);
        Assert.Contains(item.TargetEvaluations, evaluation =>
            evaluation.Target.TagValue == "Child"
            && evaluation.ObservedState
            && !evaluation.EffectiveState);
        Assert.Contains(item.TargetEvaluations, evaluation =>
            evaluation.Target.TagValue == "Added"
            && !evaluation.ObservedState
            && evaluation.EffectiveState);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
    }

    [Fact]
    public async Task ConfirmedPreviewPersistsAndQueuesPlanEquivalentToPreview()
    {
        var itemId = new Guid("d7a3fb4a-3788-4fe6-8494-f823d985653a");
        var administratorId = new Guid("375b1eb9-0b07-4828-8021-0be1c8664596");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 6 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var observed = new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Target"],
            directCollectionIds: []);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new FixedStateReader(observed),
            statusStore,
            dispatcher: dispatcher);
        var candidate = AuthoritativeCandidate("Target", "Absent");
        var previewResult = await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true);
        var authorization = Assert.IsType<ConfigurationPreviewAuthorization>(previewResult.Authorization);

        var result = await service
            .ConfirmAsync(candidate, administratorId, authorization.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(7, result.ActiveRevision);
        Assert.Equal(7, persistence.Current.Revision);
        Assert.Equal(1, persistence.SaveCount);
        var reconciliationId = Assert.IsType<Guid>(result.ReconciliationId);
        Assert.Equal(BackgroundReconciliationState.Queued, statusStore.Get(reconciliationId)?.State);
        Assert.True(dispatcher.Reader.TryRead(out var request));
        Assert.True(request.UsesPrecomputedPlans);
        var executionPlan = request.PrecomputedPlans[itemId];
        var previewMutation = Assert.Single(Assert.Single(authorization.Preview.Items).Mutations);
        var executionMutation = Assert.Single(executionPlan.Mutations);
        Assert.Equal(previewMutation.Kind, executionMutation.Kind);
        Assert.Equal(previewMutation.Target.TagValue, Assert.IsType<TagNode>(executionMutation.Target).Value);
    }

    [Fact]
    public async Task ConfirmedConfigurationRemainsActiveAfterPrecomputedPlanPartiallyFails()
    {
        var firstItemId = new Guid("260c834a-54ce-40ae-a8bd-f05c0c6a1467");
        var secondItemId = new Guid("c2cb2462-8780-41e3-a2bf-696325392e16");
        var administratorId = new Guid("e3141b31-1319-4622-b73c-8425c7c3b7ba");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 15 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var candidate = AuthoritativeCandidate("Target", "Absent");
        using var service = CreateService(
            persistence,
            new FixedCatalog([firstItemId, secondItemId], []),
            new FixedStateReader(
                State(firstItemId, ["Target"]),
                State(secondItemId, ["Target"])),
            statusStore,
            dispatcher: dispatcher);
        var preview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        var result = await service
            .ConfirmAsync(candidate, administratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);
        var quarantine = new CollectingFailureQuarantine();
        var configuration = Assert.IsType<MappingConfiguration>(
            PluginConfigurationMapper.ToDomain(candidate).Configuration);
        using var worker = new ConfigurationReconciliationWorker(
            dispatcher,
            statusStore,
            new ItemReconciler(
                new FixedMappingProvider(configuration),
                new RejectingStateReader(),
                new SelectivePlanWriter(secondItemId)),
            new PassThroughOperationalMappingProvider(),
            new ReconciliationExecutionGate(),
            quarantine,
            NullLogger<ConfigurationReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
        var reconciliationId = Assert.IsType<Guid>(result.ReconciliationId);
        await WaitUntilAsync(() =>
            statusStore.Get(reconciliationId)?.State == BackgroundReconciliationState.PartiallyFailed)
            .ConfigureAwait(true);
        await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(16, persistence.Current.Revision);
        Assert.Equal("Target", persistence.Current.MappingGroups[0].Target.TagValue);
        Assert.Equal(1, persistence.SaveCount);
        Assert.Equal(secondItemId, Assert.Single(quarantine.ItemIds));
    }

    [Fact]
    public async Task ChangedRemovalSetRejectsConfirmationWithoutSavingOrQueueing()
    {
        var itemId = new Guid("2b1f61a8-6cf2-4c8b-8ad4-fca4aba1c68b");
        var administratorId = new Guid("fdcb7db6-8b68-4f44-82c5-7769bb23b9f1");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 2 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var stateReader = new MutableStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Series,
            directTags: ["Target"],
            directCollectionIds: []));
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            stateReader,
            statusStore,
            dispatcher: dispatcher);
        var candidate = AuthoritativeCandidate("Target", "Absent");
        var preview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        stateReader.State = new ObservedItemState(
            itemId,
            EligibleItemKind.Series,
            directTags: [],
            directCollectionIds: []);

        var result = await service
            .ConfirmAsync(candidate, administratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.RequiresPreview, result.Outcome);
        Assert.Equal(2, result.ActiveRevision);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
        Assert.False(dispatcher.Reader.TryRead(out _));
    }

    [Fact]
    public async Task ChangedRemovalTupleIsRejectedEvenWhenRemovalCountIsUnchanged()
    {
        var firstItemId = new Guid("c3704892-ee4d-4019-a7f7-2e556128fd38");
        var secondItemId = new Guid("3277055b-35ea-4e49-8c89-27f4e75094b8");
        var administratorId = new Guid("7e7c5360-cfa5-408c-a96e-d5aa271fce34");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 12 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var stateReader = new MutableStateSetReader(
            State(firstItemId, ["Target"]),
            State(secondItemId, []));
        using var service = CreateService(
            persistence,
            new FixedCatalog([firstItemId, secondItemId], []),
            stateReader,
            statusStore,
            dispatcher: dispatcher);
        var candidate = AuthoritativeCandidate("Target", "Absent");
        var preview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        stateReader.Replace(
            State(firstItemId, []),
            State(secondItemId, ["Target"]));

        var result = await service
            .ConfirmAsync(candidate, administratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.RequiresPreview, result.Outcome);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(0, statusStore.Count);
        Assert.False(dispatcher.Reader.TryRead(out _));
    }

    [Fact]
    public async Task AdditionOnlyDriftDoesNotInvalidateAuthorizedRemovalSet()
    {
        var itemId = new Guid("77804a12-4706-4863-a665-cdbfa6e73045");
        var administratorId = new Guid("95bc158a-df26-46e3-938d-c52aeccaa31f");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 8 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var stateReader = new MutableStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Removed"],
            directCollectionIds: []));
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            stateReader,
            statusStore);
        var candidate = new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag("Removed"), [Tag("Absent")], MappingPolicy.Authoritative),
                Group(Tag("Added"), [Tag("New Source")], MappingPolicy.Additive),
            ],
        };
        var preview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        stateReader.State = new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Removed", "New Source"],
            directCollectionIds: []);

        var result = await service
            .ConfirmAsync(candidate, administratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, result.Outcome);
        Assert.Equal(9, result.ActiveRevision);
        Assert.Equal(1, persistence.SaveCount);
    }

    [Fact]
    public async Task AuthorizationIsBoundToAdministratorCandidateAndActiveRevision()
    {
        var itemId = new Guid("b3899219-2f80-4cdf-b7ae-7bb6fed09cd3");
        var administratorId = new Guid("e5e1896f-19a7-44ec-a109-e37181232e85");
        var otherAdministratorId = new Guid("d296ec6c-5e91-4a56-af59-702ed01887bc");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 4 });
        var statusStore = new BackgroundReconciliationStatusStore();
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new FixedStateReader(new ObservedItemState(
                itemId,
                EligibleItemKind.Movie,
                directTags: ["Target"],
                directCollectionIds: [])),
            statusStore);
        var candidate = AuthoritativeCandidate("Target", "Absent");
        var preview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);

        var otherAdministrator = await service
            .ConfirmAsync(candidate, otherAdministratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);
        var changedCandidate = AuthoritativeCandidate("Target", "Different absent source");
        var changedCandidateResult = await service
            .ConfirmAsync(changedCandidate, administratorId, preview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.InvalidAuthorization, otherAdministrator.Outcome);
        Assert.Equal(ConfigurationActivationOutcome.RequiresPreview, changedCandidateResult.Outcome);
        Assert.Equal(0, persistence.SaveCount);

        var revisionPreview = Assert.IsType<ConfigurationPreviewAuthorization>((await service
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        persistence.Replace(new PluginConfiguration { Revision = 5 });
        var changedRevision = await service
            .ConfirmAsync(candidate, administratorId, revisionPreview.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.RequiresPreview, changedRevision.Outcome);
        Assert.Equal(0, persistence.SaveCount);
    }

    [Fact]
    public async Task AuthorizationExpiresIsSingleUseAndDoesNotSurviveServiceRestart()
    {
        var itemId = new Guid("26fa2672-f877-4863-88f0-e730ad87b0c9");
        var administratorId = new Guid("a0af524b-598d-42f7-bf8c-a3b668212961");
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 4 });
        var statusStore = new BackgroundReconciliationStatusStore();
        var catalog = new FixedCatalog([itemId], []);
        var stateReader = new FixedStateReader(new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Target"],
            directCollectionIds: []));
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        var candidate = AuthoritativeCandidate("Target", "Absent");
        string expiredAuthorization;
        string restartAuthorization;
        using (var firstService = CreateService(
            persistence,
            catalog,
            stateReader,
            statusStore,
            timeProvider: time))
        {
            expiredAuthorization = Assert.IsType<ConfigurationPreviewAuthorization>((await firstService
                .PreviewAsync(candidate, administratorId, CancellationToken.None)
                .ConfigureAwait(true)).Authorization).Authorization;
            time.Advance(TimeSpan.FromMinutes(10));
            var expired = await firstService
                .ConfirmAsync(candidate, administratorId, expiredAuthorization, CancellationToken.None)
                .ConfigureAwait(true);
            Assert.Equal(ConfigurationActivationOutcome.InvalidAuthorization, expired.Outcome);

            restartAuthorization = Assert.IsType<ConfigurationPreviewAuthorization>((await firstService
                .PreviewAsync(candidate, administratorId, CancellationToken.None)
                .ConfigureAwait(true)).Authorization).Authorization;
        }

        using var restartedService = CreateService(
            persistence,
            catalog,
            stateReader,
            statusStore,
            timeProvider: time);
        var afterRestart = await restartedService
            .ConfirmAsync(candidate, administratorId, restartAuthorization, CancellationToken.None)
            .ConfigureAwait(true);
        Assert.Equal(ConfigurationActivationOutcome.InvalidAuthorization, afterRestart.Outcome);

        var fresh = Assert.IsType<ConfigurationPreviewAuthorization>((await restartedService
            .PreviewAsync(candidate, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        var accepted = await restartedService
            .ConfirmAsync(candidate, administratorId, fresh.Authorization, CancellationToken.None)
            .ConfigureAwait(true);
        var reused = await restartedService
            .ConfirmAsync(candidate, administratorId, fresh.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, accepted.Outcome);
        Assert.Equal(ConfigurationActivationOutcome.InvalidAuthorization, reused.Outcome);
        Assert.Equal(1, persistence.SaveCount);
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

    [Theory]
    [InlineData(-1, 20, 10)]
    [InlineData(25, -1, 10)]
    [InlineData(25, 101, 10)]
    [InlineData(25, 20, 0)]
    [InlineData(25, 20, 9)]
    public async Task InvalidDestructiveLimitsDoNotReplaceActiveConfiguration(
        int maximumItems,
        int maximumPercentage,
        int populationFloor)
    {
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 5 });
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            new BackgroundReconciliationStatusStore());
        var candidate = new PluginConfiguration
        {
            DestructiveMaximumAffectedItems = maximumItems,
            DestructiveMaximumRemovalPercentage = maximumPercentage,
            DestructiveMinimumAssignmentPopulation = populationFloor,
        };

        var result = await service.ActivateAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, result.Outcome);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(5, persistence.Current.Revision);
    }

    [Fact]
    public async Task DisablingCircuitBreakerRequiresAcknowledgmentAndPersistsAcceptedLimits()
    {
        var persistence = new RecordingConfigurationPersistence(new PluginConfiguration { Revision = 5 });
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new FixedStateReader(),
            new BackgroundReconciliationStatusStore());
        var unacknowledged = new PluginConfiguration
        {
            DestructiveCircuitBreakerEnabled = false,
        };

        var rejected = await service
            .ActivateAsync(unacknowledged, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, rejected.Outcome);
        Assert.Equal(0, persistence.SaveCount);

        unacknowledged.DestructiveCircuitBreakerDisableAcknowledged = true;
        unacknowledged.DestructiveMaximumAffectedItems = 5;
        unacknowledged.DestructiveMaximumRemovalPercentage = 35;
        unacknowledged.DestructiveMinimumAssignmentPopulation = 20;
        var accepted = await service
            .ActivateAsync(unacknowledged, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, accepted.Outcome);
        Assert.False(persistence.Current.DestructiveCircuitBreakerEnabled);
        Assert.True(persistence.Current.DestructiveCircuitBreakerDisableAcknowledged);
        Assert.Equal(5, persistence.Current.DestructiveMaximumAffectedItems);
        Assert.Equal(35, persistence.Current.DestructiveMaximumRemovalPercentage);
        Assert.Equal(20, persistence.Current.DestructiveMinimumAssignmentPopulation);

        var whileDisabled = new PluginConfiguration
        {
            DestructiveCircuitBreakerEnabled = false,
            DestructiveMaximumAffectedItems = 6,
            DestructiveMaximumRemovalPercentage = 40,
            DestructiveMinimumAssignmentPopulation = 25,
        };
        var updatedWhileDisabled = await service
            .ActivateAsync(whileDisabled, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, updatedWhileDisabled.Outcome);
        Assert.True(persistence.Current.DestructiveCircuitBreakerDisableAcknowledged);

        var reenabled = new PluginConfiguration
        {
            DestructiveCircuitBreakerEnabled = true,
            DestructiveCircuitBreakerDisableAcknowledged = true,
        };
        var reenabledResult = await service
            .ActivateAsync(reenabled, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Accepted, reenabledResult.Outcome);
        Assert.True(persistence.Current.DestructiveCircuitBreakerEnabled);
        Assert.False(persistence.Current.DestructiveCircuitBreakerDisableAcknowledged);

        var unacknowledgedSecondDisable = new PluginConfiguration
        {
            DestructiveCircuitBreakerEnabled = false,
        };
        var secondDisable = await service
            .ActivateAsync(unacknowledgedSecondDisable, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(ConfigurationActivationOutcome.Invalid, secondDisable.Outcome);
        Assert.True(persistence.Current.DestructiveCircuitBreakerEnabled);
    }

    private static ConfigurationActivationService CreateService(
        RecordingConfigurationPersistence persistence,
        FixedCatalog catalog,
        IItemStateReader stateReader,
        BackgroundReconciliationStatusStore statusStore,
        ReconciliationExecutionGate? executionGate = null,
        ConfigurationReconciliationDispatcher? dispatcher = null,
        IItemTitleProvider? itemTitleProvider = null,
        TimeProvider? timeProvider = null)
    {
        return new ConfigurationActivationService(
            persistence,
            catalog,
            stateReader,
            itemTitleProvider ?? new TestItemTitleProvider(),
            dispatcher ?? new ConfigurationReconciliationDispatcher(statusStore),
            statusStore,
            executionGate ?? new ReconciliationExecutionGate(),
            timeProvider ?? TimeProvider.System);
    }

    private static PluginConfiguration AuthoritativeCandidate(string target, string source)
    {
        return new PluginConfiguration
        {
            MappingGroups =
            [
                Group(Tag(target), [Tag(source)], MappingPolicy.Authoritative),
            ],
        };
    }

    private static ObservedItemState State(Guid itemId, string[] tags)
    {
        return new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: tags,
            directCollectionIds: []);
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

        public void Replace(PluginConfiguration configuration)
        {
            Current = configuration;
        }

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

    private sealed class MutableStateReader : IItemStateReader
    {
        public MutableStateReader(ObservedItemState state)
        {
            State = state;
        }

        public ObservedItemState State { get; set; }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ObservedItemState?>(State.ItemId == itemId ? State : null);
        }
    }

    private sealed class RejectingStateReader : IItemStateReader
    {
        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("A precomputed request must not re-read or replan item state.");
        }
    }

    private sealed class SelectivePlanWriter : IPlanWriter
    {
        private readonly Guid _failingItemId;

        public SelectivePlanWriter(Guid failingItemId)
        {
            _failingItemId = failingItemId;
        }

        public Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
        {
            if (plan.ItemId == _failingItemId)
            {
                throw new InvalidOperationException("Injected precomputed-plan failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class MutableStateSetReader : IItemStateReader
    {
        private Dictionary<Guid, ObservedItemState> _states;

        public MutableStateSetReader(params ObservedItemState[] states)
        {
            _states = states.ToDictionary(state => state.ItemId);
        }

        public void Replace(params ObservedItemState[] states)
        {
            _states = states.ToDictionary(state => state.ItemId);
        }

        public Task<ObservedItemState?> ReadAsync(
            Guid itemId,
            MappingConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_states.GetValueOrDefault(itemId));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
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

    private sealed class CollectingFailureQuarantine : IFailedItemQuarantine
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
