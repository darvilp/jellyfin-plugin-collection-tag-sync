using System;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Configuration;

public sealed class PluginConfigurationMapperTests
{
    [Fact]
    public void MapsBothDirectionsMixedSourcesAndFanShapes()
    {
        var animationId = new Guid("ce3c1238-727b-4b12-9c61-e9ab4c558977");
        var kidsId = new Guid("e76ee162-9661-45c9-8787-9b6f8a48e65b");
        var configuration = new PluginConfiguration
        {
            MappingGroups =
            [
                new MappingGroupConfiguration
                {
                    Target = Collection(animationId, "Animation"),
                    Sources = [Tag(" Waltney "), Tag("Blooth")],
                    Policy = MappingPolicy.Additive,
                    IsEnabled = true,
                },
                new MappingGroupConfiguration
                {
                    Target = Tag("Family"),
                    Sources = [Collection(animationId, "Renamed display data"), Tag("Kid-Approved")],
                    Policy = MappingPolicy.Authoritative,
                    IsEnabled = true,
                },
                new MappingGroupConfiguration
                {
                    Target = Collection(kidsId, "Kids"),
                    Sources = [Tag("Family")],
                    Policy = MappingPolicy.Additive,
                    IsEnabled = true,
                },
                new MappingGroupConfiguration
                {
                    Target = Tag("Archive"),
                    Sources = [Tag("Family")],
                    Policy = MappingPolicy.Additive,
                    IsEnabled = false,
                },
            ],
        };

        var result = PluginConfigurationMapper.ToDomain(configuration);

        var domain = Assert.IsType<MappingConfiguration>(result.Configuration);
        Assert.Empty(result.Errors);
        Assert.Equal(4, domain.Groups.Count);
        Assert.Equal("Waltney", Assert.IsType<TagNode>(domain.Groups[0].Sources[0]).Value);
        Assert.Equal(animationId, Assert.IsType<CollectionNode>(domain.Groups[1].Sources[0]).Id);
        Assert.Equal(MappingPolicy.Authoritative, domain.Groups[1].Policy);
        Assert.False(domain.Groups[3].IsEnabled);
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
}
