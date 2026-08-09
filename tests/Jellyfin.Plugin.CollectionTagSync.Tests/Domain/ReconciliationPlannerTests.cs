using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class ReconciliationPlannerTests
{
    [Fact]
    public void PlanAddsMissingAdditiveCollectionSupportedByTag()
    {
        var itemId = new Guid("e0a04bba-6d09-41e7-9d92-38f3ec36c6ec");
        var animationId = new Guid("a29b8bd5-952a-4bcd-b07e-ed6d610340d1");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Additive,
                isEnabled: true));
        var observed = new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Equal(itemId, plan.ItemId);
        var evaluation = Assert.Single(plan.TargetEvaluations);
        Assert.False(evaluation.ObservedState);
        Assert.True(evaluation.EffectiveState);
        Assert.Equal(MappingPolicy.Additive, evaluation.Policy);
        Assert.Equal("Waltney", Assert.IsType<TagNode>(Assert.Single(evaluation.SupportingSources)).Value);
        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.AddCollectionMembership, mutation.Kind);
        Assert.Equal(animationId, Assert.IsType<CollectionNode>(mutation.Target).Id);
        Assert.Equal(MappingPolicy.Additive, mutation.Policy);
    }

    [Fact]
    public void PlanRemovesEveryCaseEquivalentAuthoritativeTagVariant()
    {
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new TagNodeDefinition("kid-approved"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("91ae5467-593d-4d3d-ab74-c59315a6c7f6"),
            EligibleItemKind.Series,
            directTags: ["Kid-Approved", "KID-APPROVED"],
            directCollectionIds: []);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.RemoveTag, mutation.Kind);
        Assert.Equal(["KID-APPROVED", "Kid-Approved"], mutation.TagValues);
        Assert.Empty(mutation.SupportingSources);
    }

    [Fact]
    public void PlanAddsConfiguredTrimmedTagSpellingWhenAbsent()
    {
        var familyId = new Guid("5d69a9a2-b06e-46ce-b5f6-f905de228bca");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new TagNodeDefinition(" kid-approved "),
                [new CollectionNodeDefinition(familyId, "Family")],
                MappingPolicy.Additive,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("79fba07b-fb2b-46d9-81f1-d1cf1cc5c63b"),
            EligibleItemKind.Movie,
            directTags: [],
            directCollectionIds: [familyId]);

        var mutation = Assert.Single(ReconciliationPlanner.Plan(configuration, observed).Mutations);

        Assert.Equal(PlannedMutationKind.AddTag, mutation.Kind);
        Assert.Equal(["kid-approved"], mutation.TagValues);
    }

    [Fact]
    public void PlanDoesNotRewriteExistingCaseEquivalentTag()
    {
        var familyId = new Guid("b5b69e5e-9efb-4166-9b19-7a85b7486369");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new TagNodeDefinition("kid-approved"),
                [new CollectionNodeDefinition(familyId, "Family")],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("29810f14-3209-42d7-a7e8-b61d24a89ac2"),
            EligibleItemKind.Series,
            directTags: ["Kid-Approved"],
            directCollectionIds: [familyId]);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Empty(plan.Mutations);
        var evaluation = Assert.Single(plan.TargetEvaluations);
        Assert.True(evaluation.ObservedState);
        Assert.True(evaluation.EffectiveState);
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, PlannedMutationKind.AddCollectionMembership)]
    [InlineData(true, false, null)]
    [InlineData(true, true, null)]
    public void PlanImplementsAdditiveTruthTable(
        bool observedTarget,
        bool observedSource,
        PlannedMutationKind? expectedMutation)
    {
        var animationId = new Guid("7e30e6aa-9c3d-45a7-9469-c492048a772f");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Additive,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("353c1301-d6a9-451d-984a-767c043cce44"),
            EligibleItemKind.Movie,
            directTags: observedSource ? ["Waltney"] : [],
            directCollectionIds: observedTarget ? [animationId] : []);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Equal(expectedMutation, plan.Mutations.SingleOrDefault()?.Kind);
        Assert.DoesNotContain(plan.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.RemoveCollectionMembership);
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, PlannedMutationKind.AddCollectionMembership)]
    [InlineData(true, false, PlannedMutationKind.RemoveCollectionMembership)]
    [InlineData(true, true, null)]
    public void PlanImplementsAuthoritativeTruthTable(
        bool observedTarget,
        bool observedSource,
        PlannedMutationKind? expectedMutation)
    {
        var animationId = new Guid("020d86c6-003a-4f35-bbd9-2be50844c27e");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("335e229c-b683-4988-a1b2-5d4ad0ba7994"),
            EligibleItemKind.Series,
            directTags: observedSource ? ["Waltney"] : [],
            directCollectionIds: observedTarget ? [animationId] : []);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Equal(expectedMutation, plan.Mutations.SingleOrDefault()?.Kind);
    }

    [Fact]
    public void PlanUsesOrAcrossMixedSources()
    {
        var familyId = new Guid("5f7e5921-62d3-4c23-a901-1536d702ee3d");
        var kidsId = new Guid("a2cc6144-66b8-42f9-8cc3-af2eb4bbce49");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(kidsId, "Kids"),
                [
                    new TagNodeDefinition("Waltney"),
                    new CollectionNodeDefinition(familyId, "Family")
                ],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("da0712d3-d78b-4111-aa6b-1e706facf825"),
            EligibleItemKind.Movie,
            directTags: [],
            directCollectionIds: [familyId, kidsId]);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Empty(plan.Mutations);
        var supportingSource = Assert.Single(Assert.Single(plan.TargetEvaluations).SupportingSources);
        Assert.Equal(familyId, Assert.IsType<CollectionNode>(supportingSource).Id);
    }

    [Fact]
    public void PlanAllowsOneSourceToSupportSeveralTargetsAndIgnoresDisabledGroup()
    {
        var animationId = new Guid("11111111-1111-1111-1111-111111111111");
        var kidsId = new Guid("22222222-2222-2222-2222-222222222222");
        var disabledId = new Guid("33333333-3333-3333-3333-333333333333");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Additive,
                isEnabled: true),
            new MappingGroupDefinition(
                new CollectionNodeDefinition(kidsId, "Kids"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Authoritative,
                isEnabled: true),
            new MappingGroupDefinition(
                new CollectionNodeDefinition(disabledId, "Disabled"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Authoritative,
                isEnabled: false));
        var observed = new ObservedItemState(
            new Guid("ece3642d-a32c-46ee-ab71-0b0cf84257d5"),
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        Assert.Equal(
            [animationId, kidsId],
            plan.Mutations.Select(mutation => Assert.IsType<CollectionNode>(mutation.Target).Id));
        Assert.DoesNotContain(
            plan.TargetEvaluations,
            evaluation => evaluation.Target is CollectionNode collection && collection.Id == disabledId);
    }

    [Fact]
    public void PlanFeedsPreservedAdditiveTargetIntoDownstreamGroup()
    {
        var animationId = new Guid("f7fcc9cb-a7d3-4562-a20e-289a7e370a35");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Additive,
                isEnabled: true),
            new MappingGroupDefinition(
                new TagNodeDefinition("animated"),
                [new CollectionNodeDefinition(animationId, "Animation")],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("f420f18b-6356-4087-9f41-16c4ecba792c"),
            EligibleItemKind.Series,
            directTags: [],
            directCollectionIds: [animationId]);

        var plan = ReconciliationPlanner.Plan(configuration, observed);

        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.AddTag, mutation.Kind);
        Assert.Equal("animated", Assert.Single(mutation.TagValues));
    }

    [Fact]
    public void PlanSettlesMultiHopCascadeAndIsIdempotentAfterApplication()
    {
        var animationId = new Guid("64d07489-0ec4-42af-ae7f-c23d2fb2999d");
        var kidsId = new Guid("d8c9321f-721c-4b28-82d1-354ee14219d8");
        var configuration = CreateConfiguration(
            new MappingGroupDefinition(
                new CollectionNodeDefinition(animationId, "Animation"),
                [new TagNodeDefinition("Waltney")],
                MappingPolicy.Additive,
                isEnabled: true),
            new MappingGroupDefinition(
                new TagNodeDefinition("animated"),
                [new CollectionNodeDefinition(animationId, "Animation")],
                MappingPolicy.Authoritative,
                isEnabled: true),
            new MappingGroupDefinition(
                new CollectionNodeDefinition(kidsId, "Kids"),
                [new TagNodeDefinition("animated")],
                MappingPolicy.Authoritative,
                isEnabled: true));
        var observed = new ObservedItemState(
            new Guid("41f32a27-ed7b-4004-b393-bdff9f35796e"),
            EligibleItemKind.Movie,
            directTags: ["Waltney"],
            directCollectionIds: []);

        var firstPlan = ReconciliationPlanner.Plan(configuration, observed);
        var settledState = Apply(observed, firstPlan);
        var secondPlan = ReconciliationPlanner.Plan(configuration, settledState);

        Assert.Equal(
            [
                PlannedMutationKind.AddCollectionMembership,
                PlannedMutationKind.AddTag,
                PlannedMutationKind.AddCollectionMembership,
            ],
            firstPlan.Mutations.Select(mutation => mutation.Kind));
        Assert.Empty(secondPlan.Mutations);
    }

    [Fact]
    public void PlanIsIndependentOfGroupAndSourceDefinitionOrder()
    {
        var animationId = new Guid("0aff8707-39c4-41a4-b9bb-89b7de1bbc03");
        var firstAnimationGroup = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [new TagNodeDefinition("Waltney"), new TagNodeDefinition("Blooth")],
            MappingPolicy.Authoritative,
            isEnabled: true);
        var secondAnimationGroup = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [new TagNodeDefinition("Blooth"), new TagNodeDefinition("Waltney")],
            MappingPolicy.Authoritative,
            isEnabled: true);
        var animatedGroup = new MappingGroupDefinition(
            new TagNodeDefinition("animated"),
            [new CollectionNodeDefinition(animationId, "Animation")],
            MappingPolicy.Authoritative,
            isEnabled: true);
        var firstConfiguration = CreateConfiguration(firstAnimationGroup, animatedGroup);
        var secondConfiguration = CreateConfiguration(animatedGroup, secondAnimationGroup);
        var observed = new ObservedItemState(
            new Guid("4069c760-e1a3-4299-9fb6-c956633e84a3"),
            EligibleItemKind.Movie,
            directTags: ["Waltney", "Blooth"],
            directCollectionIds: []);

        var firstPlan = ReconciliationPlanner.Plan(firstConfiguration, observed);
        var secondPlan = ReconciliationPlanner.Plan(secondConfiguration, observed);

        Assert.Equal(Describe(firstPlan), Describe(secondPlan));
    }

    private static IEnumerable<string> Describe(ReconciliationPlan plan)
    {
        return plan.Mutations.Select(mutation =>
            $"{mutation.Kind}|{mutation.Target.DisplayLabel}|"
            + $"{string.Join(',', mutation.SupportingSources.Select(source => source.DisplayLabel))}|"
            + string.Join(',', mutation.TagValues));
    }

    private static ObservedItemState Apply(ObservedItemState observed, ReconciliationPlan plan)
    {
        var tags = observed.DirectTags.ToList();
        var collectionIds = observed.DirectCollectionIds.ToHashSet();
        foreach (var mutation in plan.Mutations)
        {
            switch (mutation.Kind)
            {
                case PlannedMutationKind.AddTag:
                    tags.Add(Assert.Single(mutation.TagValues));
                    break;
                case PlannedMutationKind.RemoveTag:
                    tags.RemoveAll(value => mutation.TagValues.Contains(value, StringComparer.Ordinal));
                    break;
                case PlannedMutationKind.AddCollectionMembership:
                    collectionIds.Add(Assert.IsType<CollectionNode>(mutation.Target).Id);
                    break;
                case PlannedMutationKind.RemoveCollectionMembership:
                    collectionIds.Remove(Assert.IsType<CollectionNode>(mutation.Target).Id);
                    break;
                default:
                    throw new InvalidOperationException("Unknown mutation kind.");
            }
        }

        return new ObservedItemState(
            observed.ItemId,
            observed.ItemKind,
            tags,
            collectionIds);
    }

    private static MappingConfiguration CreateConfiguration(params MappingGroupDefinition[] definitions)
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(definitions).Configuration);
    }
}
