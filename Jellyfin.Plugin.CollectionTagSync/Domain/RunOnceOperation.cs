using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes one validated mapping-shaped operation that is never persisted.
/// </summary>
public sealed class RunOnceOperation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceOperation"/> class.
    /// </summary>
    /// <param name="target">The one-time target.</param>
    /// <param name="sources">The one-time sources.</param>
    /// <param name="policy">The one-time mapping policy.</param>
    public RunOnceOperation(Node target, IEnumerable<Node> sources, MappingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sources);

        Target = target;
        Sources = Array.AsReadOnly([.. sources]);
        Policy = policy;
    }

    /// <summary>Gets the one-time target.</summary>
    public Node Target { get; }

    /// <summary>Gets the one-time sources.</summary>
    public IReadOnlyList<Node> Sources { get; }

    /// <summary>Gets the one-time mapping policy.</summary>
    public MappingPolicy Policy { get; }
}
