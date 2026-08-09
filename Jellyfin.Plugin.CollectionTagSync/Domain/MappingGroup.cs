using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents a validated immutable mapping group.
/// </summary>
public sealed class MappingGroup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingGroup"/> class.
    /// </summary>
    /// <param name="target">The validated target.</param>
    /// <param name="sources">The validated sources.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="isEnabled">Whether the group is enabled.</param>
    internal MappingGroup(Node target, IEnumerable<Node> sources, MappingPolicy policy, bool isEnabled)
    {
        Target = target;
        Sources = Array.AsReadOnly([.. sources]);
        Policy = policy;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Gets the single target.
    /// </summary>
    public Node Target { get; }

    /// <summary>
    /// Gets the sources.
    /// </summary>
    public IReadOnlyList<Node> Sources { get; }

    /// <summary>
    /// Gets the target policy.
    /// </summary>
    public MappingPolicy Policy { get; }

    /// <summary>
    /// Gets a value indicating whether the group is enabled.
    /// </summary>
    public bool IsEnabled { get; }
}
