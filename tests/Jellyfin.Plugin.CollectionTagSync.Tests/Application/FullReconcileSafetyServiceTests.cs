using System;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class FullReconcileSafetyServiceTests
{
    [Fact]
    public void PausedPreviewIncludesItemAdditionsRemovalsCascadesAndFinalSettledState()
    {
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
        [
            new MappingGroupDefinition(
                new TagNodeDefinition("Blooth-Remove"),
                [new TagNodeDefinition("Absent")],
                MappingPolicy.Authoritative,
                isEnabled: true),
            new MappingGroupDefinition(
                new TagNodeDefinition("Waltney-Intermediate"),
                [new TagNodeDefinition("Waltney-Source")],
                MappingPolicy.Additive,
                isEnabled: true),
            new MappingGroupDefinition(
                new TagNodeDefinition("Waltney-Cascade"),
                [new TagNodeDefinition("Waltney-Intermediate")],
                MappingPolicy.Additive,
                isEnabled: true),
        ]).Configuration);
        var plans = Enumerable.Range(0, 26)
            .Select(index => ReconciliationPlanner.Plan(
                configuration,
                State(index, index == 0
                    ? ["Blooth-Remove", "Waltney-Source"]
                    : ["Blooth-Remove"])))
            .ToArray();
        var persistence = new RecordingPersistence(new PluginConfiguration { Revision = 4 });
        var service = new FullReconcileSafetyService(persistence, TimeProvider.System);

        var decision = service.Evaluate(
            new Guid("005811c3-c464-4d67-b03d-61fcfec30d78"),
            [FullReconcileRequestReason.Manual],
            plans.Length,
            plans,
            confirmation: null);

        Assert.Equal(FullReconcileSafetyDecision.Paused, decision);
        var preview = Assert.IsType<PausedFullReconcileConfiguration>(
            persistence.Current.PausedFullReconcile);
        Assert.Equal(26, preview.Items.Length);
        var first = preview.Items.Single(item => item.ItemId == plans[0].ItemId);
        Assert.Contains(first.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.RemoveTag
            && mutation.Target.TagValue == "Blooth-Remove");
        Assert.Contains(first.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.TagValue == "Waltney-Intermediate");
        Assert.Contains(first.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.TagValue == "Waltney-Cascade");
        var cascadedFinalState = Assert.Single(first.TargetEvaluations, evaluation =>
            evaluation.Target.TagValue == "Waltney-Cascade");
        Assert.False(cascadedFinalState.ObservedState);
        Assert.True(cascadedFinalState.EffectiveState);
        Assert.Contains(cascadedFinalState.SupportingSources, source =>
            source.TagValue == "Waltney-Intermediate");
    }

    [Fact]
    public void AdditionOnlyDriftDoesNotInvalidateAuthorizedRemovalSet()
    {
        var configuration = Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
        [
            new MappingGroupDefinition(
                new TagNodeDefinition("Blooth-Remove"),
                [new TagNodeDefinition("Absent")],
                MappingPolicy.Authoritative,
                isEnabled: true),
            new MappingGroupDefinition(
                new TagNodeDefinition("Waltney-Addition"),
                [new TagNodeDefinition("Waltney-Source")],
                MappingPolicy.Additive,
                isEnabled: true),
        ]).Configuration);
        var initialPlans = Enumerable.Range(0, 26)
            .Select(index => ReconciliationPlanner.Plan(
                configuration,
                State(index, ["Blooth-Remove"])))
            .ToArray();
        var persistence = new RecordingPersistence(new PluginConfiguration { Revision = 9 });
        var service = new FullReconcileSafetyService(persistence, TimeProvider.System);
        var pausedRunId = new Guid("97e633ca-da66-41c1-88e5-6a08fabf5d5d");
        Assert.Equal(
            FullReconcileSafetyDecision.Paused,
            service.Evaluate(
                pausedRunId,
                [FullReconcileRequestReason.Manual],
                initialPlans.Length,
                initialPlans,
                confirmation: null));
        var administratorId = new Guid("9a903c85-5599-465e-9f10-6b6847234d4a");
        var authorization = Assert.IsType<FullReconcilePreviewAuthorization>(
            service.CreatePreviewAuthorization(pausedRunId, administratorId));
        var confirmation = Assert.IsType<FullReconcileConfirmation>(service.ConsumeAuthorization(
            pausedRunId,
            administratorId,
            authorization.Authorization));
        var freshPlans = initialPlans
            .Select((plan, index) => index == 0
                ? ReconciliationPlanner.Plan(
                    configuration,
                    State(index, ["Blooth-Remove", "Waltney-Source"]))
                : plan)
            .ToArray();

        var decision = service.Evaluate(
            new Guid("160fcdf3-d2f5-46c8-b46c-0f927774df96"),
            [FullReconcileRequestReason.Manual],
            freshPlans.Length,
            freshPlans,
            confirmation);

        Assert.Equal(FullReconcileSafetyDecision.Proceed, decision);
        Assert.Null(persistence.Current.PausedFullReconcile);
        Assert.Contains(freshPlans[0].Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.Equals(new TagNode("Waltney-Addition")));
    }

    [Fact]
    public void AuthorizationIsAdministratorBoundSingleUseAndExpiresAfterTenMinutes()
    {
        var runId = new Guid("33e6e159-5744-433f-a667-c306435d7bc3");
        var administratorId = new Guid("43343272-324c-4829-be9c-45c1178bc94e");
        var otherAdministratorId = new Guid("eeb05e56-b8ca-4507-a5ee-cfe4590ea9fb");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        var persistence = new RecordingPersistence(PausedConfiguration(runId));
        var service = new FullReconcileSafetyService(persistence, time);
        var first = Assert.IsType<FullReconcilePreviewAuthorization>(
            service.CreatePreviewAuthorization(runId, administratorId));

        Assert.Null(service.ConsumeAuthorization(runId, otherAdministratorId, first.Authorization));
        Assert.NotNull(service.ConsumeAuthorization(runId, administratorId, first.Authorization));
        Assert.Null(service.ConsumeAuthorization(runId, administratorId, first.Authorization));

        var expiring = Assert.IsType<FullReconcilePreviewAuthorization>(
            service.CreatePreviewAuthorization(runId, administratorId));
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.Null(service.ConsumeAuthorization(runId, administratorId, expiring.Authorization));
    }

    [Fact]
    public void RestartInvalidatesAuthorizationButRetainsNonExecutablePreviewDiagnostics()
    {
        var runId = new Guid("46fa44ed-a398-4f8b-90d1-6e6241216556");
        var administratorId = new Guid("9a5f7b09-7eb0-4e1d-ac14-4a617705503f");
        var persistence = new RecordingPersistence(PausedConfiguration(runId));
        var beforeRestart = new FullReconcileSafetyService(persistence, TimeProvider.System);
        var authorization = Assert.IsType<FullReconcilePreviewAuthorization>(
            beforeRestart.CreatePreviewAuthorization(runId, administratorId));

        var afterRestart = new FullReconcileSafetyService(persistence, TimeProvider.System);

        Assert.Null(afterRestart.ConsumeAuthorization(
            runId,
            administratorId,
            authorization.Authorization));
        Assert.Equal(runId, persistence.Current.PausedFullReconcile?.RunId);
        Assert.Single(persistence.Current.PausedFullReconcile?.Removals ?? []);
    }

    [Fact]
    public void RestartHydratesAwaitingApprovalStatusFromPersistedDiagnostics()
    {
        var runId = new Guid("892cf903-49d6-485b-b4e2-2bb1fb449bbd");
        var persistence = new RecordingPersistence(PausedConfiguration(runId));

        var status = new FullReconcileStatusStore(persistence).Current;

        Assert.Equal(runId, status.Id);
        Assert.Equal(FullReconcileState.AwaitingApproval, status.State);
        Assert.Equal([FullReconcileRequestReason.Manual], status.Reasons);
        Assert.Equal(1, status.TotalItemCount);
        Assert.Equal(0, status.SucceededItemCount);
    }

    private static PluginConfiguration PausedConfiguration(Guid runId)
    {
        return new PluginConfiguration
        {
            Revision = 7,
            PausedFullReconcile = new PausedFullReconcileConfiguration
            {
                RunId = runId,
                ConfigurationRevision = 7,
                CreatedUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
                Reasons = [FullReconcileRequestReason.Manual],
                TotalItemCount = 1,
                UniqueAffectedItemCount = 1,
                Removals =
                [
                    new PausedFullReconcileRemovalConfiguration
                    {
                        ItemId = new Guid("acb6996c-f3f2-46f7-8afb-5593f7374456"),
                        Target = new MappingNodeConfiguration
                        {
                            Kind = MappingNodeKind.Tag,
                            TagValue = "Waltney-Safe",
                        },
                        Kind = PlannedMutationKind.RemoveTag,
                    },
                ],
            },
        };
    }

    private static ObservedItemState State(int index, string[] tags)
    {
        return new ObservedItemState(
            Guid.Parse($"40000000-0000-0000-0000-{index + 1:D12}"),
            EligibleItemKind.Movie,
            tags,
            []);
    }

    private sealed class RecordingPersistence : IPluginConfigurationPersistence
    {
        public RecordingPersistence(PluginConfiguration current)
        {
            Current = current;
        }

        public PluginConfiguration Current { get; private set; }

        public void Save(PluginConfiguration configuration)
        {
            Current = configuration;
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
            _utcNow += duration;
        }
    }
}
