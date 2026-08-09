using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents validated immutable mapping configuration.
/// </summary>
public sealed class MappingConfiguration
{
    private MappingConfiguration(IEnumerable<MappingGroup> groups)
    {
        Groups = Array.AsReadOnly([.. groups]);
    }

    /// <summary>
    /// Gets the validated mapping groups.
    /// </summary>
    public IReadOnlyList<MappingGroup> Groups { get; }

    /// <summary>
    /// Validates and normalizes candidate mapping groups.
    /// </summary>
    /// <param name="definitions">The complete candidate mapping configuration.</param>
    /// <returns>A validated configuration or explicit diagnostics.</returns>
    public static MappingConfigurationValidationResult Create(IEnumerable<MappingGroupDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var groups = new List<MappingGroup>();
        var errors = new List<MappingValidationError>();
        var targetIdentities = new HashSet<Node>();
        var groupIndex = 0;
        foreach (var definition in definitions)
        {
            if (definition.Sources.Count == 0)
            {
                errors.Add(new MappingValidationError(
                    MappingValidationErrorCode.NoSources,
                    "A mapping group must contain at least one source.",
                    groupIndex,
                    null));
            }

            if (!Enum.IsDefined(definition.Policy))
            {
                errors.Add(new MappingValidationError(
                    MappingValidationErrorCode.InvalidPolicy,
                    "A mapping group policy must be Additive or Authoritative.",
                    groupIndex,
                    null));
            }

            var target = CreateNode(definition.Target, groupIndex, null, errors);
            if (target is not null && !targetIdentities.Add(target))
            {
                errors.Add(new MappingValidationError(
                    MappingValidationErrorCode.DuplicateTarget,
                    "Each normalized target can belong to only one persisted mapping group.",
                    groupIndex,
                    null));
            }

            var sources = new List<Node>();
            var sourceIdentities = new HashSet<Node>();
            for (var sourceIndex = 0; sourceIndex < definition.Sources.Count; sourceIndex++)
            {
                var source = CreateNode(definition.Sources[sourceIndex], groupIndex, sourceIndex, errors);
                if (source is not null)
                {
                    if (!sourceIdentities.Add(source))
                    {
                        errors.Add(new MappingValidationError(
                            MappingValidationErrorCode.DuplicateSource,
                            "A mapping group cannot contain the same normalized source more than once.",
                            groupIndex,
                            sourceIndex));
                    }

                    if (target is not null && target.Equals(source))
                    {
                        errors.Add(new MappingValidationError(
                            MappingValidationErrorCode.SelfSource,
                            "A mapping group cannot contain its own target as a source.",
                            groupIndex,
                            sourceIndex));
                    }

                    sources.Add(source);
                }
            }

            if (target is not null && sources.Count > 0 && sources.Count == definition.Sources.Count)
            {
                groups.Add(new MappingGroup(target, sources, definition.Policy, definition.IsEnabled));
            }

            groupIndex++;
        }

        return errors.Count == 0
            ? new MappingConfigurationValidationResult(new MappingConfiguration(groups), errors)
            : new MappingConfigurationValidationResult(null, errors);
    }

    private static Node? CreateNode(
        NodeDefinition definition,
        int groupIndex,
        int? sourceIndex,
        ICollection<MappingValidationError> errors)
    {
        return definition switch
        {
            TagNodeDefinition tag => CreateTagNode(tag, groupIndex, sourceIndex, errors),
            CollectionNodeDefinition collection => CreateCollectionNode(collection, groupIndex, sourceIndex, errors),
            _ => throw new InvalidOperationException("Unknown node definition type."),
        };
    }

    private static CollectionNode? CreateCollectionNode(
        CollectionNodeDefinition definition,
        int groupIndex,
        int? sourceIndex,
        ICollection<MappingValidationError> errors)
    {
        if (definition.Id == Guid.Empty)
        {
            errors.Add(new MappingValidationError(
                MappingValidationErrorCode.InvalidCollectionId,
                "A configured collection must have a non-empty Jellyfin identifier.",
                groupIndex,
                sourceIndex));
            return null;
        }

        return new CollectionNode(definition.Id, definition.DisplayName);
    }

    private static TagNode? CreateTagNode(
        TagNodeDefinition definition,
        int groupIndex,
        int? sourceIndex,
        ICollection<MappingValidationError> errors)
    {
        var value = definition.Value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(new MappingValidationError(
                MappingValidationErrorCode.EmptyTag,
                "A configured tag must contain at least one non-whitespace character.",
                groupIndex,
                sourceIndex));
            return null;
        }

        return new TagNode(value);
    }
}
