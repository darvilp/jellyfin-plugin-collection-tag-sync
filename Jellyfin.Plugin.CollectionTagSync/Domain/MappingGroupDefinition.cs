using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes a candidate mapping group before validation.
/// </summary>
public sealed class MappingGroupDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingGroupDefinition"/> class.
    /// </summary>
    /// <param name="target">The single target definition.</param>
    /// <param name="sources">The source definitions.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="isEnabled">Whether the group participates in continuous synchronization.</param>
    public MappingGroupDefinition(
        NodeDefinition target,
        IEnumerable<NodeDefinition> sources,
        MappingPolicy policy,
        bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sources);

        Target = target;
        Sources = Array.AsReadOnly([.. sources]);
        Policy = policy;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Gets the target definition.
    /// </summary>
    public NodeDefinition Target { get; }

    /// <summary>
    /// Gets the source definitions.
    /// </summary>
    public IReadOnlyList<NodeDefinition> Sources { get; }

    /// <summary>
    /// Gets the target policy.
    /// </summary>
    public MappingPolicy Policy { get; }

    /// <summary>
    /// Gets a value indicating whether the group is enabled.
    /// </summary>
    public bool IsEnabled { get; }
}
