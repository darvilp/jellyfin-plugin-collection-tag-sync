using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Domain;

public sealed class MappingConfigurationTests
{
    [Fact]
    public void CreateReturnsNormalizedImmutableGroupForValidMixedSources()
    {
        var collectionId = new Guid("b2bd92b4-b880-46de-a450-dff1a3d1f763");
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition(" Kid-Approved "),
            [
                new TagNodeDefinition("Waltney"),
                new CollectionNodeDefinition(collectionId, "Family Favorites")
            ],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        var configuration = Assert.IsType<MappingConfiguration>(result.Configuration);
        var group = Assert.Single(configuration.Groups);
        Assert.Equal(MappingPolicy.Additive, group.Policy);
        Assert.True(group.IsEnabled);
        Assert.Equal("Kid-Approved", Assert.IsType<TagNode>(group.Target).Value);
        Assert.Equal("Waltney", Assert.IsType<TagNode>(group.Sources[0]).Value);
        var collection = Assert.IsType<CollectionNode>(group.Sources[1]);
        Assert.Equal(collectionId, collection.Id);
        Assert.Equal("Family Favorites", collection.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsTagThatIsEmptyAfterTrimming(string? configuredValue)
    {
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition(configuredValue),
            [new CollectionNodeDefinition(new Guid("768eb387-6cf8-4db9-a5a9-90b23f1415ed"))],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        Assert.Null(result.Configuration);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.EmptyTag, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Null(error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsGroupWithoutSources()
    {
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition("Kid-Approved"),
            [],
            MappingPolicy.Authoritative,
            isEnabled: false);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.NoSources, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Null(error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsCaseEquivalentTagAsItsOwnSource()
    {
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition(" kid-approved "),
            [new TagNodeDefinition("KID-APPROVED")],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.SelfSource, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Equal(0, error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsDuplicateSourcesAfterTagNormalization()
    {
        var definition = new MappingGroupDefinition(
            new CollectionNodeDefinition(new Guid("23dd6226-f0ca-4de8-9fea-10ba2fbaa078"), "Kids"),
            [
                new TagNodeDefinition("Waltney"),
                new TagNodeDefinition(" wAlTnEy ")
            ],
            MappingPolicy.Authoritative,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.DuplicateSource, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Equal(1, error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsCaseEquivalentTargetReservedByDisabledGroup()
    {
        var firstDefinition = new MappingGroupDefinition(
            new TagNodeDefinition("Kid-Approved"),
            [new CollectionNodeDefinition(new Guid("11f6f184-aecb-4dcc-90f7-72e9c28afcbd"), "Kids")],
            MappingPolicy.Additive,
            isEnabled: false);
        var secondDefinition = new MappingGroupDefinition(
            new TagNodeDefinition(" kid-approved "),
            [new CollectionNodeDefinition(new Guid("d4750654-6507-45bc-a98b-c1cb7686fd6b"), "Family")],
            MappingPolicy.Authoritative,
            isEnabled: true);

        var result = MappingConfiguration.Create([firstDefinition, secondDefinition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.DuplicateTarget, error.Code);
        Assert.Equal(1, error.GroupIndex);
        Assert.Null(error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsEmptyCollectionIdentity()
    {
        var definition = new MappingGroupDefinition(
            new CollectionNodeDefinition(Guid.Empty, "Not a collection"),
            [new TagNodeDefinition("Waltney")],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.InvalidCollectionId, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Null(error.SourceIndex);
    }

    [Fact]
    public void CreateRejectsPolicyOutsideSupportedValues()
    {
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition("Kid-Approved"),
            [new TagNodeDefinition("Waltney")],
            (MappingPolicy)99,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.InvalidPolicy, error.Code);
        Assert.Equal(0, error.GroupIndex);
        Assert.Null(error.SourceIndex);
    }

    [Fact]
    public void CreateTreatsCollectionDisplayNamesAsNonIdentityData()
    {
        var collectionId = new Guid("12e7f3dc-b40a-47f3-a042-61b95bbaca50");
        var firstDefinition = new MappingGroupDefinition(
            new CollectionNodeDefinition(collectionId, "Kids"),
            [new TagNodeDefinition("Waltney")],
            MappingPolicy.Additive,
            isEnabled: true);
        var secondDefinition = new MappingGroupDefinition(
            new CollectionNodeDefinition(collectionId, "Renamed Kids"),
            [new TagNodeDefinition("Blooth")],
            MappingPolicy.Authoritative,
            isEnabled: true);

        var result = MappingConfiguration.Create([firstDefinition, secondDefinition]);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(MappingValidationErrorCode.DuplicateTarget, error.Code);
        Assert.Equal(1, error.GroupIndex);
    }

    [Fact]
    public void CreateKeepsDiacriticTagVariantsDistinct()
    {
        var firstDefinition = new MappingGroupDefinition(
            new TagNodeDefinition("Café"),
            [new CollectionNodeDefinition(new Guid("e8aeeea6-20dc-48cc-b74e-496522676b0e"))],
            MappingPolicy.Additive,
            isEnabled: true);
        var secondDefinition = new MappingGroupDefinition(
            new TagNodeDefinition("Cafe"),
            [new CollectionNodeDefinition(new Guid("76f6580d-7d9b-491e-9802-21266f0e1a10"))],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([firstDefinition, secondDefinition]);

        Assert.True(result.IsValid);
        Assert.Equal(2, Assert.IsType<MappingConfiguration>(result.Configuration).Groups.Count);
    }

    [Fact]
    public void CreateAllowsOneSourceToParticipateInSeveralGroups()
    {
        var firstDefinition = new MappingGroupDefinition(
            new TagNodeDefinition("Kid-Approved"),
            [new TagNodeDefinition("Waltney")],
            MappingPolicy.Additive,
            isEnabled: true);
        var secondDefinition = new MappingGroupDefinition(
            new CollectionNodeDefinition(new Guid("d36ec90b-ac08-4611-9da2-d08010399579"), "Animation"),
            [new TagNodeDefinition(" waltney ")],
            MappingPolicy.Authoritative,
            isEnabled: true);

        var result = MappingConfiguration.Create([firstDefinition, secondDefinition]);

        Assert.True(result.IsValid);
        Assert.Equal(2, Assert.IsType<MappingConfiguration>(result.Configuration).Groups.Count);
    }

    [Fact]
    public void CreateTreatsSameCollectionGuidAsSelfSourceDespiteDifferentName()
    {
        var collectionId = new Guid("78e40be2-6677-4f62-97d0-f001447ccded");
        var definition = new MappingGroupDefinition(
            new CollectionNodeDefinition(collectionId, "Current name"),
            [new CollectionNodeDefinition(collectionId, "Stale name")],
            MappingPolicy.Additive,
            isEnabled: true);

        var result = MappingConfiguration.Create([definition]);

        Assert.False(result.IsValid);
        Assert.Equal(MappingValidationErrorCode.SelfSource, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void DefinitionCopiesCallerOwnedSourceCollection()
    {
        var sources = new List<NodeDefinition>
        {
            new TagNodeDefinition("Waltney"),
        };
        var definition = new MappingGroupDefinition(
            new TagNodeDefinition("Kid-Approved"),
            sources,
            MappingPolicy.Additive,
            isEnabled: true);

        sources.Add(new TagNodeDefinition("Blooth"));
        var result = MappingConfiguration.Create([definition]);

        var configuration = Assert.IsType<MappingConfiguration>(result.Configuration);
        Assert.Single(Assert.Single(configuration.Groups).Sources);
    }
}
