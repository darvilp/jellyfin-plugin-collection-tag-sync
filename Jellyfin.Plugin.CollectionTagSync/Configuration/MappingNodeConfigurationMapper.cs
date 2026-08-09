using System;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Maps and clones serializer-friendly synchronization nodes.
/// </summary>
internal static class MappingNodeConfigurationMapper
{
    /// <summary>Maps one validated immutable node to a serializer-friendly DTO.</summary>
    /// <param name="node">The validated node.</param>
    /// <returns>The serializer-friendly node.</returns>
    public static MappingNodeConfiguration FromDomain(Node node)
    {
        return node switch
        {
            TagNode tag => new MappingNodeConfiguration
            {
                Kind = MappingNodeKind.Tag,
                TagValue = tag.Value,
            },
            CollectionNode collection => new MappingNodeConfiguration
            {
                Kind = MappingNodeKind.Collection,
                CollectionId = collection.Id,
                CollectionDisplayName = collection.DisplayName ?? string.Empty,
            },
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }

    /// <summary>Creates an independent serializer-friendly node clone.</summary>
    /// <param name="node">The source node.</param>
    /// <returns>The independent node clone.</returns>
    public static MappingNodeConfiguration Clone(MappingNodeConfiguration? node)
    {
        node ??= new MappingNodeConfiguration();
        return new MappingNodeConfiguration
        {
            Kind = node.Kind,
            TagValue = node.TagValue,
            CollectionId = node.CollectionId,
            CollectionDisplayName = node.CollectionDisplayName,
        };
    }
}
