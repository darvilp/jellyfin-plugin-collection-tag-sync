using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes one planned candidate-configuration addition or removal.
/// </summary>
public sealed class ConfigurationMutationPreview
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationMutationPreview"/> class.
    /// </summary>
    /// <param name="kind">The direct mutation kind.</param>
    /// <param name="target">The serializer-friendly target.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="supportingSources">The effective sources supporting the target.</param>
    /// <param name="tagValues">The exact tag spellings to add or remove.</param>
    internal ConfigurationMutationPreview(
        PlannedMutationKind kind,
        MappingNodeConfiguration target,
        MappingPolicy policy,
        IEnumerable<MappingNodeConfiguration> supportingSources,
        IEnumerable<string> tagValues)
    {
        Kind = kind;
        Target = target;
        Policy = policy;
        SupportingSources = [.. supportingSources];
        TagValues = [.. tagValues];
    }

    /// <summary>Gets the direct mutation kind.</summary>
    public PlannedMutationKind Kind { get; }

    /// <summary>Gets the serializer-friendly target.</summary>
    public MappingNodeConfiguration Target { get; }

    /// <summary>Gets the target policy.</summary>
    public MappingPolicy Policy { get; }

    /// <summary>Gets the effective sources supporting the target.</summary>
    public IReadOnlyList<MappingNodeConfiguration> SupportingSources { get; }

    /// <summary>Gets the exact tag spellings to add or remove.</summary>
    public IReadOnlyList<string> TagValues { get; }
}
