using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class RunOnceServiceTests
{
    private static readonly Guid AnimationId = new("b41eb888-1d58-4772-b6f0-f7de07ad5bef");
    private static readonly Guid KidsId = new("ba85c3f6-a02f-419a-8e76-01f00a8acbb4");

    [Fact]
    public async Task PreviewAndConfirmationBootstrapCollectionWithoutPersistingRunOnceEdge()
    {
        var itemId = new Guid("a331db78-a48f-45a9-812d-11561095b86d");
        var administratorId = new Guid("0162a620-0c4b-4a71-8584-74e91858de63");
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 4,
            MappingGroups =
            [
                Group(Tag("animated"), [Collection(AnimationId, "Animation")], MappingPolicy.Additive),
                Group(Collection(KidsId, "Kids"), [Tag("animated")], MappingPolicy.Additive),
            ],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], [AnimationId, KidsId]),
            new MutableStateReader(State(itemId, EligibleItemKind.Movie, ["Waltney"], [])),
            dispatcher,
            statusStore,
            itemTitleProvider: new TestItemTitleProvider((itemId, "Waltney Adventure")));
        var operation = Operation(
            Collection(AnimationId, "Animation"),
            [Tag("Waltney")],
            MappingPolicy.Additive);

        var preview = await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOncePreviewOutcome.Ready, preview.Outcome);
        var authorization = Assert.IsType<RunOncePreviewAuthorization>(preview.Authorization);
        var item = Assert.Single(authorization.Preview.Items);
        Assert.Equal("Waltney Adventure", item.ItemTitle);
        Assert.Equal(3, item.Mutations.Count);
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddCollectionMembership
            && mutation.Target.CollectionId == AnimationId);
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.TagValue == "animated");
        Assert.Contains(item.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddCollectionMembership
            && mutation.Target.CollectionId == KidsId);
        Assert.Equal(0, persistence.SaveCount);

        var execution = await service
            .ConfirmAsync(operation, administratorId, authorization.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.Accepted, execution.Outcome);
        Assert.Equal(4, execution.ActiveRevision);
        Assert.Equal(0, persistence.SaveCount);
        Assert.Equal(2, persistence.Current.MappingGroups.Length);
        Assert.DoesNotContain(persistence.Current.MappingGroups, group =>
            group.Target.CollectionId == AnimationId);
        Assert.True(dispatcher.Reader.TryRead(out var queued));
        Assert.True(queued.UsesPrecomputedPlans);
        Assert.Equal(3, queued.PrecomputedPlans[itemId].Mutations.Count);
    }

    [Fact]
    public async Task PreviewMarksOnlyDirectOperationTargetChangesAsExcludable()
    {
        var directTargetChange = new Guid("98a126dd-e82d-4317-b830-7236ad50f57c");
        var cascadeOnlyChange = new Guid("e303f9e8-9133-4575-b3fd-a4c4de3fc186");
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 5,
            MappingGroups =
            [
                Group(Tag("animated"), [Collection(AnimationId, "Animation")], MappingPolicy.Additive),
            ],
        });
        using var service = CreateService(
            persistence,
            new FixedCatalog([directTargetChange, cascadeOnlyChange], [AnimationId]),
            new MutableStateReader(
                State(directTargetChange, EligibleItemKind.Movie, ["Waltney"], []),
                State(cascadeOnlyChange, EligibleItemKind.Series, [], [AnimationId])),
            new ConfigurationReconciliationDispatcher(new BackgroundReconciliationStatusStore()),
            new BackgroundReconciliationStatusStore());

        var result = await service
            .PreviewAsync(
                Operation(
                    Collection(AnimationId, "Animation"),
                    [Tag("Waltney")],
                    MappingPolicy.Additive),
                new Guid("584ead5d-52c8-456b-962f-e5a89c2dd7d6"),
                CancellationToken.None)
            .ConfigureAwait(true);

        var authorization = Assert.IsType<RunOncePreviewAuthorization>(result.Authorization);
        Assert.Equal(2, authorization.Preview.Items.Count);
        Assert.Equal(directTargetChange, Assert.Single(authorization.ExcludableItemIds));
    }

    [Theory]
    [InlineData(true, RunOncePreviewOutcome.Invalid)]
    [InlineData(false, RunOncePreviewOutcome.Ready)]
    public async Task EnabledContinuousTargetConflictsButDisabledTargetDoesNot(
        bool enabled,
        RunOncePreviewOutcome expectedOutcome)
    {
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 2,
            MappingGroups =
            [
                Group(Tag("Target"), [Tag("Source")], MappingPolicy.Additive, enabled),
            ],
        });
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new MutableStateReader(),
            new ConfigurationReconciliationDispatcher(new BackgroundReconciliationStatusStore()),
            new BackgroundReconciliationStatusStore());

        var result = await service
            .PreviewAsync(
                Operation(Tag("Target"), [Tag("Waltney")], MappingPolicy.Additive),
                new Guid("c7a29c64-ebaf-4f7e-821a-b780cdd82d31"),
                CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(expectedOutcome, result.Outcome);
        if (enabled)
        {
            Assert.Contains(result.ValidationErrors, error =>
                error.Code == RunOnceValidationErrorCode.TargetConflict);
        }
    }

    [Fact]
    public async Task ExclusionRetainsDirectTargetRecomputesCascadeAndIsNeverPersisted()
    {
        var itemId = new Guid("eb9e25e7-c2c1-4142-94b9-33a9f62853ea");
        var administratorId = new Guid("1052928e-2572-41a9-973b-1bb709e41aed");
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 9,
            MappingGroups =
            [
                Group(Tag("animated"), [Collection(AnimationId, "Animation")], MappingPolicy.Authoritative),
            ],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], [AnimationId]),
            new MutableStateReader(State(itemId, EligibleItemKind.Series, [], [AnimationId])),
            dispatcher,
            statusStore);
        var excluded = Operation(
            Collection(AnimationId, "Animation"),
            [Tag("Absent")],
            MappingPolicy.Authoritative,
            [itemId]);

        var preview = await service
            .PreviewAsync(excluded, administratorId, CancellationToken.None)
            .ConfigureAwait(true);

        var authorization = Assert.IsType<RunOncePreviewAuthorization>(preview.Authorization);
        var item = Assert.Single(authorization.Preview.Items);
        var mutation = Assert.Single(item.Mutations);
        Assert.Equal(PlannedMutationKind.AddTag, mutation.Kind);
        Assert.Equal("animated", mutation.Target.TagValue);
        Assert.DoesNotContain(item.Mutations, candidate =>
            candidate.Target.CollectionId == AnimationId);

        var mismatched = await service
            .ConfirmAsync(
                Operation(
                    Collection(AnimationId, "Animation"),
                    [Tag("Absent")],
                    MappingPolicy.Authoritative),
                administratorId,
                authorization.Authorization,
                CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.RequiresPreview, mismatched.Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));

        authorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(excluded, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        var accepted = await service
            .ConfirmAsync(excluded, administratorId, authorization.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.Accepted, accepted.Outcome);
        Assert.Equal(0, persistence.SaveCount);
        Assert.True(dispatcher.Reader.TryRead(out var queued));
        Assert.Equal(PlannedMutationKind.AddTag, Assert.Single(queued.PrecomputedPlans[itemId].Mutations).Kind);
    }

    [Fact]
    public async Task AuthoritativePreviewSelectsSameMovieAndSeriesChangesAsWholeLibrary()
    {
        var addMovie = new Guid("be88475d-60ff-4f4d-b0f2-c9c7994f7d32");
        var removeSeries = new Guid("3e2cfcd6-5c86-4767-b384-7e927a7a5293");
        var unchangedMovie = new Guid("f1d6332c-9e79-487b-8bc4-6c49acda1aa1");
        var unchangedSeries = new Guid("1b44664c-b1e6-44eb-8c47-bff5b5425784");
        var reader = new MutableStateReader(
            State(addMovie, EligibleItemKind.Movie, ["Waltney-Source"], []),
            State(removeSeries, EligibleItemKind.Series, ["Blooth-Target"], []),
            State(unchangedMovie, EligibleItemKind.Movie, ["Waltney-Source", "Blooth-Target"], []),
            State(unchangedSeries, EligibleItemKind.Series, [], []));
        using var service = CreateService(
            new RecordingPersistence(new PluginConfiguration { Revision = 1 }),
            new FixedCatalog(
                [addMovie, removeSeries, unchangedMovie, unchangedSeries],
                []),
            reader,
            new ConfigurationReconciliationDispatcher(new BackgroundReconciliationStatusStore()),
            new BackgroundReconciliationStatusStore());

        var result = await service
            .PreviewAsync(
                Operation(Tag("Blooth-Target"), [Tag("Waltney-Source")], MappingPolicy.Authoritative),
                new Guid("09a102aa-2f97-4df1-8fe0-2bb044812597"),
                CancellationToken.None)
            .ConfigureAwait(true);

        var preview = Assert.IsType<RunOncePreviewAuthorization>(result.Authorization).Preview;
        Assert.Equal(4, preview.TotalItemCount);
        Assert.Equal(
            new[] { addMovie, removeSeries }.Order(),
            preview.Items.Select(item => item.ItemId).Order());
        Assert.Contains(preview.Items, item =>
            item.ItemId == addMovie
            && item.ItemKind == EligibleItemKind.Movie
            && Assert.Single(item.Mutations).Kind == PlannedMutationKind.AddTag);
        Assert.Contains(preview.Items, item =>
            item.ItemId == removeSeries
            && item.ItemKind == EligibleItemKind.Series
            && Assert.Single(item.Mutations).Kind == PlannedMutationKind.RemoveTag);
    }

    [Fact]
    public async Task ChangedRemovalSetRejectsConfirmationButAdditionOnlyDriftIsAccepted()
    {
        var removalId = new Guid("79c7a0b5-d4fd-42ce-84a6-43a3545a88a1");
        var additionId = new Guid("808032f6-3cea-450f-854e-7e85b0201019");
        var administratorId = new Guid("4fb801af-bec4-474e-a394-cdc1820d7ad8");
        var reader = new MutableStateReader(
            State(removalId, EligibleItemKind.Movie, ["Target"], []),
            State(additionId, EligibleItemKind.Series, [], []));
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            new RecordingPersistence(new PluginConfiguration { Revision = 7 }),
            new FixedCatalog([removalId, additionId], []),
            reader,
            dispatcher,
            statusStore);
        var operation = Operation(Tag("Target"), [Tag("Source")], MappingPolicy.Authoritative);
        var first = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        reader.Replace(
            State(removalId, EligibleItemKind.Movie, [], []),
            State(additionId, EligibleItemKind.Series, [], []));

        var rejected = await service
            .ConfirmAsync(operation, administratorId, first.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.RequiresPreview, rejected.Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));

        reader.Replace(
            State(removalId, EligibleItemKind.Movie, ["Target"], []),
            State(additionId, EligibleItemKind.Series, [], []));
        var second = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        reader.Replace(
            State(removalId, EligibleItemKind.Movie, ["Target"], []),
            State(additionId, EligibleItemKind.Series, ["Source"], []));

        var accepted = await service
            .ConfirmAsync(operation, administratorId, second.Authorization, CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.Accepted, accepted.Outcome);
        Assert.True(dispatcher.Reader.TryRead(out var queued));
        Assert.Equal(2, queued.PrecomputedPlans.Count);
        Assert.Contains(queued.PrecomputedPlans[removalId].Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.RemoveTag);
        Assert.Contains(queued.PrecomputedPlans[additionId].Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag);
    }

    [Fact]
    public async Task AuthorizationIsAdministratorRevisionExpirySingleUseAndRestartBound()
    {
        var itemId = new Guid("c70257fe-7184-446f-9bb7-4740d8c52389");
        var administratorId = new Guid("bdecc9d7-c451-4525-a6cb-c9ef4086a308");
        var otherAdministratorId = new Guid("a32fd337-9c80-4cd6-b92d-71f4d72579b2");
        var persistence = new RecordingPersistence(new PluginConfiguration { Revision = 10 });
        var reader = new MutableStateReader(State(itemId, EligibleItemKind.Movie, ["Source"], []));
        var catalog = new FixedCatalog([itemId], []);
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        var operation = Operation(Tag("Target"), [Tag("Source")], MappingPolicy.Additive);
        using var service = CreateService(persistence, catalog, reader, dispatcher, statusStore, time);
        var otherAdmin = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);

        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await service
                .ConfirmAsync(operation, otherAdministratorId, otherAdmin.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);

        var revision = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        persistence.Replace(new PluginConfiguration { Revision = 11 });
        Assert.Equal(
            RunOnceExecutionOutcome.RequiresPreview,
            (await service
                .ConfirmAsync(operation, administratorId, revision.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);
        persistence.Replace(new PluginConfiguration { Revision = 10 });

        var expired = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        time.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await service
                .ConfirmAsync(operation, administratorId, expired.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);

        var restart = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        using var restarted = CreateService(persistence, catalog, reader, dispatcher, statusStore, time);
        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await restarted
                .ConfirmAsync(operation, administratorId, restart.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);

        var singleUse = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewAsync(operation, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        Assert.Equal(
            RunOnceExecutionOutcome.Accepted,
            (await service
                .ConfirmAsync(operation, administratorId, singleUse.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);
        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await service
                .ConfirmAsync(operation, administratorId, singleUse.Authorization, CancellationToken.None)
                .ConfigureAwait(true)).Outcome);
    }

    [Fact]
    public async Task SavedGroupsAreValidatedPersistedEditedAndDeletedWithoutChangingContinuousRevision()
    {
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 12,
            MappingGroups = [Group(Tag("Continuous"), [Tag("Source")], MappingPolicy.Additive)],
        });
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new MutableStateReader(),
            new ConfigurationReconciliationDispatcher(new BackgroundReconciliationStatusStore()),
            new BackgroundReconciliationStatusStore());
        var candidate = SavedGroup(
            Guid.Empty,
            Tag("Run target"),
            [Tag("Run source")],
            MappingPolicy.Additive);

        var created = await service.SaveGroupAsync(candidate, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(RunOnceGroupSaveOutcome.Saved, created.Outcome);
        var createdGroup = Assert.IsType<RunOnceGroupConfiguration>(created.Group);
        Assert.NotEqual(Guid.Empty, createdGroup.Id);
        Assert.Equal(12, persistence.Current.Revision);
        Assert.Single(persistence.Current.MappingGroups);
        Assert.Equal(createdGroup.Id, Assert.Single(persistence.Current.RunOnceGroups).Id);

        var edited = await service.SaveGroupAsync(
            SavedGroup(
                createdGroup.Id,
                Tag("Edited target"),
                [Tag("Edited source")],
                MappingPolicy.Authoritative),
            CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(RunOnceGroupSaveOutcome.Saved, edited.Outcome);
        var persisted = Assert.Single(persistence.Current.RunOnceGroups);
        Assert.Equal("Edited target", persisted.Target.TagValue);
        Assert.Equal(MappingPolicy.Authoritative, persisted.Policy);
        Assert.True(await service.DeleteGroupAsync(createdGroup.Id, CancellationToken.None).ConfigureAwait(true));
        Assert.Empty(persistence.Current.RunOnceGroups);
        Assert.False(await service.DeleteGroupAsync(createdGroup.Id, CancellationToken.None).ConfigureAwait(true));
    }

    [Fact]
    public async Task SavedGroupPreviewIsIndependentAndStaleAfterSelectionEditOrDelete()
    {
        var itemId = new Guid("d39c71ea-29be-4718-8cb6-3ba508e46715");
        var administratorId = new Guid("15d03f0a-d816-4325-aa53-df115d11dbaf");
        var groupAId = new Guid("b7e8792a-4f25-4c0e-9ce4-fe9636348c26");
        var groupBId = new Guid("c4649095-58e7-4529-8995-122f9c513e29");
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 6,
            RunOnceGroups =
            [
                SavedGroup(groupAId, Tag("Target A"), [Tag("Source A")], MappingPolicy.Additive),
                SavedGroup(groupBId, Tag("Target B"), [Tag("Source B")], MappingPolicy.Additive),
            ],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new MutableStateReader(State(itemId, EligibleItemKind.Movie, ["Source A"], [])),
            dispatcher,
            statusStore);
        var requestA = new SavedRunOnceOperationRequest { GroupId = groupAId };
        var requestB = new SavedRunOnceOperationRequest { GroupId = groupBId };

        var first = await service.PreviewSavedAsync(requestA, administratorId, CancellationToken.None)
            .ConfigureAwait(true);

        var firstAuthorization = Assert.IsType<RunOncePreviewAuthorization>(first.Authorization);
        var previewItem = Assert.Single(firstAuthorization.Preview.Items);
        var mutation = Assert.Single(previewItem.Mutations);
        Assert.Equal("Target A", mutation.Target.TagValue);
        Assert.Equal(
            RunOnceExecutionOutcome.RequiresPreview,
            (await service.ConfirmSavedAsync(
                requestB,
                administratorId,
                firstAuthorization.Authorization,
                CancellationToken.None).ConfigureAwait(true)).Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));

        var editAuthorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewSavedAsync(requestA, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        await service.SaveGroupAsync(
            SavedGroup(groupAId, Tag("Edited A"), [Tag("Source A")], MappingPolicy.Additive),
            CancellationToken.None).ConfigureAwait(true);
        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await service.ConfirmSavedAsync(
                requestA,
                administratorId,
                editAuthorization.Authorization,
                CancellationToken.None).ConfigureAwait(true)).Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));

        var deleteAuthorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewSavedAsync(requestA, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        Assert.True(await service.DeleteGroupAsync(groupAId, CancellationToken.None).ConfigureAwait(true));
        Assert.Equal(
            RunOnceExecutionOutcome.RequiresPreview,
            (await service.ConfirmSavedAsync(
                requestA,
                administratorId,
                deleteAuthorization.Authorization,
                CancellationToken.None).ConfigureAwait(true)).Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));
        Assert.Equal(groupBId, Assert.Single(persistence.Current.RunOnceGroups).Id);
    }

    [Fact]
    public async Task AnySavedGroupWriteAndOrderedSourceChangeInvalidateItsPreview()
    {
        var itemId = new Guid("e799784d-81f8-477c-8577-d553306134a0");
        var administratorId = new Guid("5954e597-8fa0-4ae3-923a-582d517fd9ac");
        var groupId = new Guid("028d2d75-48d7-470f-ac3d-e387d3f9cb3b");
        var original = SavedGroup(
            groupId,
            Tag("Target"),
            [Tag("Source A"), Tag("Source B")],
            MappingPolicy.Additive);
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 8,
            RunOnceGroups = [original],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new MutableStateReader(State(itemId, EligibleItemKind.Movie, ["Source A"], [])),
            dispatcher,
            statusStore);
        var request = new SavedRunOnceOperationRequest { GroupId = groupId };
        var savedAgainAuthorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewSavedAsync(request, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);

        await service.SaveGroupAsync(original, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(
            RunOnceExecutionOutcome.InvalidAuthorization,
            (await service.ConfirmSavedAsync(
                request,
                administratorId,
                savedAgainAuthorization.Authorization,
                CancellationToken.None).ConfigureAwait(true)).Outcome);

        var reorderedAuthorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewSavedAsync(request, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        var changed = PluginConfigurationCloner.Clone(persistence.Current);
        changed.RunOnceGroups[0].Sources = [Tag("Source B"), Tag("Source A")];
        persistence.Replace(changed);

        Assert.Equal(
            RunOnceExecutionOutcome.RequiresPreview,
            (await service.ConfirmSavedAsync(
                request,
                administratorId,
                reorderedAuthorization.Authorization,
                CancellationToken.None).ConfigureAwait(true)).Outcome);
        Assert.False(dispatcher.Reader.TryRead(out _));
    }

    [Fact]
    public async Task NonEmptyUnknownGroupIdentityCannotRecreateADeletedGroup()
    {
        var unknownId = new Guid("e1d15360-492d-49b9-b568-3fe9bd9e4925");
        var persistence = new RecordingPersistence(new PluginConfiguration());
        using var service = CreateService(
            persistence,
            new FixedCatalog([], []),
            new MutableStateReader(),
            new ConfigurationReconciliationDispatcher(new BackgroundReconciliationStatusStore()),
            new BackgroundReconciliationStatusStore());

        var result = await service.SaveGroupAsync(
            SavedGroup(unknownId, Tag("Target"), [Tag("Source")], MappingPolicy.Additive),
            CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(RunOnceGroupSaveOutcome.Invalid, result.Outcome);
        Assert.Contains(result.ValidationErrors, error => error.Code == RunOnceValidationErrorCode.MissingGroup);
        Assert.Empty(persistence.Current.RunOnceGroups);
    }

    [Fact]
    public async Task SuccessfulSavedGroupExecutionRetainsTheGroupAndDoesNotPersistExclusions()
    {
        var itemId = new Guid("1ebf5c80-b5a4-4883-a4ec-9510e25c7cb7");
        var administratorId = new Guid("37f1779b-d36d-48fc-8149-3a614b643e82");
        var groupId = new Guid("7a2a3a06-fb0a-4d3c-8fa2-6c625650fbf0");
        var group = SavedGroup(groupId, Tag("Target"), [Tag("Source")], MappingPolicy.Additive);
        var persistence = new RecordingPersistence(new PluginConfiguration
        {
            Revision = 3,
            RunOnceGroups = [group],
        });
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        using var service = CreateService(
            persistence,
            new FixedCatalog([itemId], []),
            new MutableStateReader(State(itemId, EligibleItemKind.Movie, ["Source"], [])),
            dispatcher,
            statusStore);
        var request = new SavedRunOnceOperationRequest
        {
            GroupId = groupId,
            ExcludedItemIds = [],
        };

        var authorization = Assert.IsType<RunOncePreviewAuthorization>((await service
            .PreviewSavedAsync(request, administratorId, CancellationToken.None)
            .ConfigureAwait(true)).Authorization);
        var result = await service
            .ConfirmSavedAsync(
                request,
                administratorId,
                authorization.Authorization,
                CancellationToken.None)
            .ConfigureAwait(true);

        Assert.Equal(RunOnceExecutionOutcome.Accepted, result.Outcome);
        Assert.Equal(0, persistence.SaveCount);
        var retained = Assert.Single(service.GetGroups());
        Assert.Equal(groupId, retained.Id);
        Assert.Equal("Target", retained.Target.TagValue);
        Assert.True(dispatcher.Reader.TryRead(out _));
    }

    private static RunOnceService CreateService(
        RecordingPersistence persistence,
        FixedCatalog catalog,
        IItemStateReader stateReader,
        ConfigurationReconciliationDispatcher dispatcher,
        BackgroundReconciliationStatusStore statusStore,
        TimeProvider? timeProvider = null,
        IItemTitleProvider? itemTitleProvider = null)
    {
        return new RunOnceService(
            persistence,
            catalog,
            stateReader,
            itemTitleProvider ?? new TestItemTitleProvider(),
            dispatcher,
            new ReconciliationExecutionGate(),
            timeProvider ?? TimeProvider.System);
    }

    private static RunOnceOperationRequest Operation(
        MappingNodeConfiguration target,
        MappingNodeConfiguration[] sources,
        MappingPolicy policy,
        Guid[]? excludedItemIds = null)
    {
        return new RunOnceOperationRequest
        {
            Target = target,
            Sources = sources,
            Policy = policy,
            ExcludedItemIds = excludedItemIds ?? [],
        };
    }

    private static MappingGroupConfiguration Group(
        MappingNodeConfiguration target,
        MappingNodeConfiguration[] sources,
        MappingPolicy policy,
        bool enabled = true)
    {
        return new MappingGroupConfiguration
        {
            Target = target,
            Sources = sources,
            Policy = policy,
            IsEnabled = enabled,
        };
    }

    private static RunOnceGroupConfiguration SavedGroup(
        Guid id,
        MappingNodeConfiguration target,
        MappingNodeConfiguration[] sources,
        MappingPolicy policy)
    {
        return new RunOnceGroupConfiguration
        {
            Id = id,
            Target = target,
            Sources = sources,
            Policy = policy,
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

    private static ObservedItemState State(
        Guid itemId,
        EligibleItemKind kind,
        string[] tags,
        Guid[] collections)
    {
        return new ObservedItemState(itemId, kind, tags, collections);
    }

    private sealed class RecordingPersistence : IPluginConfigurationPersistence
    {
        public RecordingPersistence(PluginConfiguration current)
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
        private readonly IReadOnlyList<Guid> _itemIds;
        private readonly HashSet<Guid> _collectionIds;

        public FixedCatalog(IEnumerable<Guid> itemIds, IEnumerable<Guid> collectionIds)
        {
            _itemIds = [.. itemIds];
            _collectionIds = [.. collectionIds];
        }

        public IReadOnlyList<Guid> GetEligibleItemIds() => _itemIds;

        public bool CollectionExists(Guid collectionId) => _collectionIds.Contains(collectionId);
    }

    private sealed class MutableStateReader : IItemStateReader
    {
        private Dictionary<Guid, ObservedItemState> _states;

        public MutableStateReader(params ObservedItemState[] states)
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
}
