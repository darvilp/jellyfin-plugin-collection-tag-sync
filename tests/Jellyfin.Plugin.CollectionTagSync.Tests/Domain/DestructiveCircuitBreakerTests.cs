using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class DestructiveCircuitBreakerTests
{
    [Theory]
    [InlineData(25, false)]
    [InlineData(26, true)]
    public void AbsoluteLimitTripsOnlyAboveConfiguredUniqueItemCount(
        int removalItemCount,
        bool expectedPause)
    {
        var target = new TagNode("Blooth-Approved");
        var plans = Enumerable.Range(0, removalItemCount)
            .Select(index => Plan(index, (target, Remove: true)))
            .ToArray();
        var limits = new DestructiveCircuitBreakerOptions(
            isEnabled: true,
            maximumAffectedItems: 25,
            maximumRemovalPercentage: 100,
            minimumAssignmentPopulation: 10);

        var result = DestructiveCircuitBreaker.Evaluate(plans, limits);

        Assert.Equal(expectedPause, result.ShouldPause);
        Assert.Equal(removalItemCount, result.UniqueAffectedItemCount);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void PercentageLimitTripsOnlyAboveConfiguredPercentage(int removals, bool expectedPause)
    {
        var target = new TagNode("Waltney-Safe");
        var plans = Enumerable.Range(0, 10)
            .Select(index => Plan(index, (target, Remove: index < removals)))
            .ToArray();
        var limits = new DestructiveCircuitBreakerOptions(
            isEnabled: true,
            maximumAffectedItems: 100,
            maximumRemovalPercentage: 20,
            minimumAssignmentPopulation: 10);

        var result = DestructiveCircuitBreaker.Evaluate(plans, limits);

        Assert.Equal(expectedPause, result.ShouldPause);
        var group = Assert.Single(result.Groups);
        Assert.Equal(10, group.CurrentAssignmentCount);
        Assert.Equal(removals, group.RemovalCount);
        Assert.Equal(expectedPause, group.ExceedsPercentageLimit);
    }

    [Fact]
    public void PercentageLimitIsNotEvaluatedBelowConfiguredPopulationFloor()
    {
        var target = new TagNode("Blooth-Classic");
        var plans = Enumerable.Range(0, 9)
            .Select(index => Plan(index, (target, Remove: index < 2)))
            .ToArray();
        var limits = new DestructiveCircuitBreakerOptions(
            isEnabled: true,
            maximumAffectedItems: 100,
            maximumRemovalPercentage: 20,
            minimumAssignmentPopulation: 10);

        var result = DestructiveCircuitBreaker.Evaluate(plans, limits);

        Assert.False(result.ShouldPause);
        Assert.False(Assert.Single(result.Groups).ExceedsPercentageLimit);
    }

    [Fact]
    public void SeveralRemovalsOnOneItemCountOnceTowardAbsoluteLimit()
    {
        var firstTarget = new TagNode("Waltney-Safe");
        var secondTarget = new TagNode("Blooth-Approved");
        var limits = new DestructiveCircuitBreakerOptions(
            isEnabled: true,
            maximumAffectedItems: 1,
            maximumRemovalPercentage: 100,
            minimumAssignmentPopulation: 10);

        var result = DestructiveCircuitBreaker.Evaluate(
            [Plan(0, (firstTarget, Remove: true), (secondTarget, Remove: true))],
            limits);

        Assert.False(result.ShouldPause);
        Assert.Equal(1, result.UniqueAffectedItemCount);
        Assert.Equal(2, result.Removals.Count);
    }

    [Fact]
    public void EitherLimitIndependentlyPausesTheEntirePlan()
    {
        var absoluteTarget = new TagNode("Blooth-Absolute");
        var absolutePlans = Enumerable.Range(0, 26)
            .Select(index => Plan(index, (absoluteTarget, Remove: true)))
            .ToArray();
        var relativeTarget = new TagNode("Waltney-Relative");
        var relativePlans = Enumerable.Range(0, 10)
            .Select(index => Plan(index + 100, (relativeTarget, Remove: index < 3)))
            .ToArray();

        var absolute = DestructiveCircuitBreaker.Evaluate(
            absolutePlans,
            new DestructiveCircuitBreakerOptions(true, 25, 100, 10));
        var relative = DestructiveCircuitBreaker.Evaluate(
            relativePlans,
            new DestructiveCircuitBreakerOptions(true, 100, 20, 10));

        Assert.True(absolute.ExceedsAbsoluteLimit);
        Assert.False(Assert.Single(absolute.Groups).ExceedsPercentageLimit);
        Assert.True(absolute.ShouldPause);
        Assert.False(relative.ExceedsAbsoluteLimit);
        Assert.True(Assert.Single(relative.Groups).ExceedsPercentageLimit);
        Assert.True(relative.ShouldPause);
    }

    private static ReconciliationPlan Plan(
        int itemNumber,
        params (Node Target, bool Remove)[] targets)
    {
        var itemId = Guid.Parse($"00000000-0000-0000-0000-{itemNumber + 1:D12}");
        var evaluations = targets.Select(target => new TargetEvaluation(
            target.Target,
            MappingPolicy.Authoritative,
            observedState: true,
            effectiveState: !target.Remove,
            supportingSources: []));
        var mutations = targets
            .Where(target => target.Remove)
            .Select(target => new PlannedMutation(
                target.Target is TagNode
                    ? PlannedMutationKind.RemoveTag
                    : PlannedMutationKind.RemoveCollectionMembership,
                target.Target,
                MappingPolicy.Authoritative,
                supportingSources: [],
                tagValues: target.Target is TagNode tag ? [tag.Value] : []));
        return new ReconciliationPlan(itemId, EligibleItemKind.Movie, evaluations, mutations);
    }
}
