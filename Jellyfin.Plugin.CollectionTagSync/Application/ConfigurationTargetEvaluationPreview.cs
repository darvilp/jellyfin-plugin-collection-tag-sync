using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes one mapped target's observed and final settled state.
/// </summary>
public sealed class ConfigurationTargetEvaluationPreview
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationTargetEvaluationPreview"/> class.
    /// </summary>
    /// <param name="target">The serializer-friendly target.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="observedState">Whether the target is directly observed.</param>
    /// <param name="effectiveState">Whether the target is present after complete settlement.</param>
    /// <param name="supportingSources">The effective sources supporting the target.</param>
    internal ConfigurationTargetEvaluationPreview(
        MappingNodeConfiguration target,
        MappingPolicy policy,
        bool observedState,
        bool effectiveState,
        IEnumerable<MappingNodeConfiguration> supportingSources)
    {
        Target = target;
        Policy = policy;
        ObservedState = observedState;
        EffectiveState = effectiveState;
        SupportingSources = [.. supportingSources];
    }

    /// <summary>Gets the serializer-friendly target.</summary>
    public MappingNodeConfiguration Target { get; }

    /// <summary>Gets the target policy.</summary>
    public MappingPolicy Policy { get; }

    /// <summary>Gets a value indicating whether the target is directly observed.</summary>
    public bool ObservedState { get; }

    /// <summary>Gets a value indicating whether the target is present after complete settlement.</summary>
    public bool EffectiveState { get; }

    /// <summary>Gets the effective sources supporting the target.</summary>
    public IReadOnlyList<MappingNodeConfiguration> SupportingSources { get; }
}
