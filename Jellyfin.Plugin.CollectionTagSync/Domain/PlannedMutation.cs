using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes one direct metadata mutation and why it is required.
/// </summary>
public sealed class PlannedMutation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlannedMutation"/> class.
    /// </summary>
    /// <param name="kind">The mutation kind.</param>
    /// <param name="target">The mapped target.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="supportingSources">The effective sources supporting the target.</param>
    /// <param name="tagValues">The exact tag spellings to add or remove.</param>
    internal PlannedMutation(
        PlannedMutationKind kind,
        Node target,
        MappingPolicy policy,
        IEnumerable<Node> supportingSources,
        IEnumerable<string> tagValues)
    {
        Kind = kind;
        Target = target;
        Policy = policy;
        SupportingSources = Array.AsReadOnly([.. supportingSources]);
        TagValues = Array.AsReadOnly([.. tagValues]);
    }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public PlannedMutationKind Kind { get; }

    /// <summary>
    /// Gets the mapped target.
    /// </summary>
    public Node Target { get; }

    /// <summary>
    /// Gets the target policy.
    /// </summary>
    public MappingPolicy Policy { get; }

    /// <summary>
    /// Gets the effective sources supporting the target.
    /// </summary>
    public IReadOnlyList<Node> SupportingSources { get; }

    /// <summary>
    /// Gets the exact tag spellings to add or remove, or an empty list for collection mutations.
    /// </summary>
    public IReadOnlyList<string> TagValues { get; }
}
