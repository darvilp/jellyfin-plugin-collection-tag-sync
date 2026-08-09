using System;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class RunOncePlannerTests
{
    private static readonly Guid AnimationId = new("376947ba-e3bf-4a20-8533-84b37e36f8e0");
    private static readonly Guid KidsId = new("7baf4215-1c24-499d-a8d5-22bd1281a2d4");

    [Fact]
    public void AdditiveBootstrapIncludesDirectTargetAndDownstreamContinuousCascade()
    {
        var animation = new CollectionNode(AnimationId, "Animation");
        var animated = new TagNode("animated");
        var kids = new CollectionNode(KidsId, "Kids");
        var active = Configuration(
            Group(animated, [animation], MappingPolicy.Additive),
            Group(kids, [animated], MappingPolicy.Additive));
        var operation = new RunOnceOperation(
            animation,
            [new TagNode("Waltney")],
            MappingPolicy.Additive);
        var itemId = new Guid("b0e84960-0367-4ad2-9be9-4051b3168da0");
        var state = State(itemId, ["Waltney"], []);

        var plan = RunOncePlanner.Plan(
            active,
            operation,
            state,
            keepCurrentTargetState: false);

        Assert.Equal(3, plan.Mutations.Count);
        Assert.Contains(plan.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddCollectionMembership
            && mutation.Target.Equals(animation));
        Assert.Contains(plan.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddTag
            && mutation.Target.Equals(animated));
        Assert.Contains(plan.Mutations, mutation =>
            mutation.Kind == PlannedMutationKind.AddCollectionMembership
            && mutation.Target.Equals(kids));
        Assert.All(plan.TargetEvaluations, evaluation => Assert.True(evaluation.EffectiveState));
    }

    [Fact]
    public void ReverseBootstrapSettlesWithoutPersistingOrMergingTheRunOnceEdge()
    {
        var animation = new CollectionNode(AnimationId, "Animation");
        var waltney = new TagNode("Waltney");
        var active = Configuration(Group(waltney, [animation], MappingPolicy.Additive));
        var operation = new RunOnceOperation(animation, [waltney], MappingPolicy.Additive);
        var state = State(
            new Guid("9abbf154-adad-4ce4-99ac-b216f6a29c3b"),
            ["Waltney"],
            []);

        var plan = RunOncePlanner.Plan(
            active,
            operation,
            state,
            keepCurrentTargetState: false);

        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.AddCollectionMembership, mutation.Kind);
        Assert.Equal(animation, mutation.Target);
        Assert.Single(active.Groups);
        Assert.Equal(waltney, active.Groups[0].Target);
    }

    [Fact]
    public void OptimizedAuthoritativeCandidatesMatchWholeLibraryMutationSemantics()
    {
        var operation = new RunOnceOperation(
            new TagNode("Blooth-Target"),
            [new TagNode("Waltney-Source")],
            MappingPolicy.Authoritative);
        var active = Configuration();
        var states = new[]
        {
            State(new Guid("7f23f799-a954-4a04-816e-f36ca0adbf0c"), ["Waltney-Source"], []),
            State(new Guid("69041926-e227-4bf6-8cdf-2681364dad42"), ["Blooth-Target"], []),
            State(new Guid("45d930f2-d36a-4644-9915-417a1ff9787e"), ["Waltney-Source", "Blooth-Target"], []),
            State(new Guid("5b20017b-4a48-4f4c-8864-f5f35f0d07ca"), [], []),
        };

        var candidateIds = RunOnceCandidateSelector.Select(
            active,
            operation,
            states,
            excludedItemIds: []);
        var optimizedMutations = states
            .Where(state => candidateIds.Contains(state.ItemId))
            .SelectMany(state => RunOncePlanner.Plan(
                active,
                operation,
                state,
                keepCurrentTargetState: false).Mutations)
            .Select(MutationKey)
            .Order()
            .ToArray();
        var wholeLibraryMutations = states
            .SelectMany(state => RunOncePlanner.Plan(
                active,
                operation,
                state,
                keepCurrentTargetState: false).Mutations)
            .Select(MutationKey)
            .Order()
            .ToArray();

        Assert.Equal(2, candidateIds.Count);
        Assert.Equal(wholeLibraryMutations, optimizedMutations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExclusionKeepsObservedDirectTargetState(bool observedTargetState)
    {
        var animation = new CollectionNode(AnimationId, "Animation");
        var operation = new RunOnceOperation(
            animation,
            [new TagNode("Waltney")],
            MappingPolicy.Authoritative);
        var state = State(
            new Guid("3c796be2-46fc-4928-a336-da3a2d9d010f"),
            observedTargetState ? [] : ["Waltney"],
            observedTargetState ? [AnimationId] : []);

        var plan = RunOncePlanner.Plan(
            Configuration(),
            operation,
            state,
            keepCurrentTargetState: true);

        Assert.Empty(plan.Mutations);
        var direct = Assert.Single(plan.TargetEvaluations);
        Assert.Equal(observedTargetState, direct.ObservedState);
        Assert.Equal(observedTargetState, direct.EffectiveState);
    }

    [Fact]
    public void ExclusionRecomputesDownstreamAndCannotSuppressCascade()
    {
        var animation = new CollectionNode(AnimationId, "Animation");
        var animated = new TagNode("animated");
        var active = Configuration(Group(animated, [animation], MappingPolicy.Authoritative));
        var operation = new RunOnceOperation(
            animation,
            [new TagNode("Absent")],
            MappingPolicy.Authoritative);
        var state = State(
            new Guid("d2c751ef-8ea0-401e-854a-1a0e18f58d0a"),
            [],
            [AnimationId]);

        var plan = RunOncePlanner.Plan(
            active,
            operation,
            state,
            keepCurrentTargetState: true);

        var mutation = Assert.Single(plan.Mutations);
        Assert.Equal(PlannedMutationKind.AddTag, mutation.Kind);
        Assert.Equal(animated, mutation.Target);
        Assert.DoesNotContain(plan.Mutations, candidate => candidate.Target.Equals(animation));
    }

    private static string MutationKey(PlannedMutation mutation)
    {
        return $"{mutation.Kind}:{mutation.Target.DisplayLabel}";
    }

    private static MappingGroupDefinition Group(
        Node target,
        Node[] sources,
        MappingPolicy policy)
    {
        return new MappingGroupDefinition(
            ToDefinition(target),
            sources.Select(ToDefinition),
            policy,
            isEnabled: true);
    }

    private static MappingConfiguration Configuration(params MappingGroupDefinition[] groups)
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(groups).Configuration);
    }

    private static NodeDefinition ToDefinition(Node node)
    {
        return node switch
        {
            TagNode tag => new TagNodeDefinition(tag.Value),
            CollectionNode collection => new CollectionNodeDefinition(collection.Id, collection.DisplayName),
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }

    private static ObservedItemState State(Guid itemId, string[] tags, Guid[] collections)
    {
        return new ObservedItemState(
            itemId,
            EligibleItemKind.Movie,
            tags,
            collections);
    }
}
