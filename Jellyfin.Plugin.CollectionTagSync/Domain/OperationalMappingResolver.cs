using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Derives a fail-closed operational graph from collection availability.
/// </summary>
public static class OperationalMappingResolver
{
    /// <summary>
    /// Resolves collection-backed groups without changing the persisted enabled state.
    /// </summary>
    /// <param name="configured">The validated persisted configuration.</param>
    /// <param name="availableCollectionIds">Collection identities that currently resolve.</param>
    /// <returns>The operational graph and persistent warning data.</returns>
    public static OperationalMappingConfiguration Resolve(
        MappingConfiguration configured,
        IEnumerable<Guid> availableCollectionIds)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(availableCollectionIds);

        var available = availableCollectionIds.ToHashSet();
        var unresolved = configured.Groups
            .Select((group, index) => new
            {
                Group = group,
                Index = index,
                Missing = group.Sources
                    .Append(group.Target)
                    .OfType<CollectionNode>()
                    .Where(collection => !available.Contains(collection.Id))
                    .Distinct()
                    .OrderBy(collection => collection.Id)
                    .ToArray(),
            })
            .Where(candidate => candidate.Group.IsEnabled && candidate.Missing.Length > 0)
            .Select(candidate => new UnresolvedMappingGroupDiagnostic(
                candidate.Index,
                candidate.Group.Target,
                candidate.Missing))
            .ToArray();
        var unresolvedIndexes = unresolved.Select(diagnostic => diagnostic.GroupIndex).ToHashSet();
        var operationalDefinitions = configured.Groups.Select((group, index) =>
            new MappingGroupDefinition(
                ToDefinition(group.Target),
                group.Sources.Select(ToDefinition),
                group.Policy,
                group.IsEnabled && !unresolvedIndexes.Contains(index)));
        var operational = MappingConfiguration.Create(operationalDefinitions).Configuration
            ?? throw new InvalidOperationException("Disabling unresolved groups produced an invalid graph.");
        return new OperationalMappingConfiguration(operational, unresolved);
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
}
