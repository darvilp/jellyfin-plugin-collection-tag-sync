using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class ContinuousGraphTests
{
    [Fact]
    public void CreateFlattensOnlyEnabledGroupsIntoActiveEdges()
    {
        var animationId = new Guid("6298f8ee-0c26-43c5-b087-575589cac475");
        var enabled = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [
                new TagNodeDefinition("Waltney"),
                new TagNodeDefinition("Blooth")
            ],
            MappingPolicy.Additive,
            isEnabled: true);
        var disabled = new MappingGroupDefinition(
            new TagNodeDefinition("animated"),
            [new CollectionNodeDefinition(animationId, "Animation")],
            MappingPolicy.Authoritative,
            isEnabled: false);

        var result = MappingConfiguration.Create([enabled, disabled]);

        var graph = Assert.IsType<MappingConfiguration>(result.Configuration).ActiveGraph;
        Assert.Collection(
            graph.Edges,
            edge =>
            {
                Assert.Equal("Waltney", Assert.IsType<TagNode>(edge.Source).Value);
                Assert.Equal(animationId, Assert.IsType<CollectionNode>(edge.Target).Id);
            },
            edge =>
            {
                Assert.Equal("Blooth", Assert.IsType<TagNode>(edge.Source).Value);
                Assert.Equal(animationId, Assert.IsType<CollectionNode>(edge.Target).Id);
            });
    }

    [Fact]
    public void CreateProducesDeterministicTopologicalOrderIndependentOfDefinitionOrder()
    {
        var animationId = new Guid("71815215-1603-44d5-957a-53ecda051631");
        var animationGroup = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [new TagNodeDefinition("Waltney"), new TagNodeDefinition("Blooth")],
            MappingPolicy.Additive,
            isEnabled: true);
        var animatedGroup = new MappingGroupDefinition(
            new TagNodeDefinition("animated"),
            [new CollectionNodeDefinition(animationId, "Animation")],
            MappingPolicy.Additive,
            isEnabled: true);

        var first = MappingConfiguration.Create([animationGroup, animatedGroup]);
        var reversedAnimationGroup = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [new TagNodeDefinition("Blooth"), new TagNodeDefinition("Waltney")],
            MappingPolicy.Additive,
            isEnabled: true);
        var second = MappingConfiguration.Create([animatedGroup, reversedAnimationGroup]);

        var expected = new[]
        {
            "Tag:Blooth",
            "Tag:Waltney",
            $"Collection:{animationId:D}",
            "Tag:animated",
        };
        Assert.Equal(expected, GetOrder(first));
        Assert.Equal(expected, GetOrder(second));
    }

    [Fact]
    public void CreateRejectsCycleWithReadableCompletePath()
    {
        var animationId = new Guid("267128a7-5c01-4a90-95bd-ae9ae6d5b919");
        var toCollection = new MappingGroupDefinition(
            new CollectionNodeDefinition(animationId, "Animation"),
            [new TagNodeDefinition("Waltney")],
            MappingPolicy.Additive,
            isEnabled: true);
        var backToTag = new MappingGroupDefinition(
            new TagNodeDefinition("Waltney"),
            [new CollectionNodeDefinition(animationId, "Animation")],
            MappingPolicy.Authoritative,
            isEnabled: true);

        var result = MappingConfiguration.Create([toCollection, backToTag]);

        Assert.False(result.IsValid);
        Assert.Null(result.Configuration);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.Cycle, error.Code);
        var expectedPath = new[]
        {
            "Tag \"Waltney\"",
            "Collection \"Animation\"",
            "Tag \"Waltney\"",
        };
        Assert.Equal(expectedPath, error.CyclePath.Select(node => node.DisplayLabel));
        Assert.Contains(string.Join("\n→ ", expectedPath), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateReportsCompleteMixedNodeMultiHopCycle()
    {
        var animationId = new Guid("7a93d918-9b29-4bf8-ae49-debc47c46573");
        var kidsId = new Guid("f833681a-b6c6-46ec-adb3-e5c0bd28e31a");
        var result = MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(animationId, "Animation"),
                    [new TagNodeDefinition("Waltney")],
                    MappingPolicy.Additive,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new TagNodeDefinition("animated"),
                    [new CollectionNodeDefinition(animationId, "Animation")],
                    MappingPolicy.Additive,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new CollectionNodeDefinition(kidsId, "Kids"),
                    [new TagNodeDefinition("animated")],
                    MappingPolicy.Additive,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new TagNodeDefinition("Waltney"),
                    [new CollectionNodeDefinition(kidsId, "Kids")],
                    MappingPolicy.Authoritative,
                    isEnabled: true),
            ]);

        var labels = Assert.Single(result.Errors).CyclePath.Select(node => node.DisplayLabel);

        Assert.Equal(
            [
                "Tag \"animated\"",
                "Collection \"Kids\"",
                "Tag \"Waltney\"",
                "Collection \"Animation\"",
                "Tag \"animated\"",
            ],
            labels);
    }

    [Fact]
    public void EnablingDisabledClosingEdgeRevalidatesCompleteGraph()
    {
        var first = new MappingGroupDefinition(
            new TagNodeDefinition("B"),
            [new TagNodeDefinition("A")],
            MappingPolicy.Additive,
            isEnabled: true);
        var second = new MappingGroupDefinition(
            new TagNodeDefinition("C"),
            [new TagNodeDefinition("B")],
            MappingPolicy.Additive,
            isEnabled: true);
        var disabledClosingEdge = new MappingGroupDefinition(
            new TagNodeDefinition("A"),
            [new TagNodeDefinition("C")],
            MappingPolicy.Additive,
            isEnabled: false);

        var whileDisabled = MappingConfiguration.Create([first, second, disabledClosingEdge]);
        var enabledClosingEdge = new MappingGroupDefinition(
            disabledClosingEdge.Target,
            disabledClosingEdge.Sources,
            disabledClosingEdge.Policy,
            isEnabled: true);
        var whenEnabled = MappingConfiguration.Create([first, second, enabledClosingEdge]);

        Assert.True(whileDisabled.IsValid);
        Assert.Equal(2, Assert.IsType<MappingConfiguration>(whileDisabled.Configuration).ActiveGraph.Edges.Count);
        Assert.False(whenEnabled.IsValid);
        Assert.Equal(
            ["Tag \"A\"", "Tag \"B\"", "Tag \"C\"", "Tag \"A\""],
            Assert.Single(whenEnabled.Errors).CyclePath.Select(node => node.DisplayLabel));
    }

    [Fact]
    public void InvalidCandidateDoesNotAlterPreviouslyValidatedGraph()
    {
        var baseline = MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("B"),
                    [new TagNodeDefinition("A")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]);
        var invalidCandidate = MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition("B"),
                    [new TagNodeDefinition("A")],
                    MappingPolicy.Additive,
                    isEnabled: true),
                new MappingGroupDefinition(
                    new TagNodeDefinition("A"),
                    [new TagNodeDefinition("B")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]);

        Assert.False(invalidCandidate.IsValid);
        Assert.Equal(
            ["Tag:A", "Tag:B"],
            GetOrder(baseline));
    }

    private static IEnumerable<string> GetOrder(MappingConfigurationValidationResult result)
    {
        return Assert.IsType<MappingConfiguration>(result.Configuration)
            .ActiveGraph
            .TopologicalOrder
            .Select(node => node switch
            {
                TagNode tag => $"Tag:{tag.Value}",
                CollectionNode collection => $"Collection:{collection.Id:D}",
                _ => throw new InvalidOperationException("Unknown node type."),
            });
    }
}
