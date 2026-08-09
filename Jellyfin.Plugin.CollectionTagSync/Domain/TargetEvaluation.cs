using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Explains the settled state of one enabled mapped target.
/// </summary>
public sealed class TargetEvaluation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TargetEvaluation"/> class.
    /// </summary>
    /// <param name="target">The mapped target.</param>
    /// <param name="policy">The target policy.</param>
    /// <param name="observedState">The target's direct observed state.</param>
    /// <param name="effectiveState">The target's final settled state.</param>
    /// <param name="supportingSources">The effective sources supporting the target.</param>
    internal TargetEvaluation(
        Node target,
        MappingPolicy policy,
        bool observedState,
        bool effectiveState,
        IEnumerable<Node> supportingSources)
    {
        Target = target;
        Policy = policy;
        ObservedState = observedState;
        EffectiveState = effectiveState;
        SupportingSources = Array.AsReadOnly([.. supportingSources]);
    }

    /// <summary>
    /// Gets the mapped target.
    /// </summary>
    public Node Target { get; }

    /// <summary>
    /// Gets the target policy.
    /// </summary>
    public MappingPolicy Policy { get; }

    /// <summary>
    /// Gets a value indicating whether the target was directly observed.
    /// </summary>
    public bool ObservedState { get; }

    /// <summary>
    /// Gets a value indicating whether the target is present in the final settled state.
    /// </summary>
    public bool EffectiveState { get; }

    /// <summary>
    /// Gets the effective sources supporting the target.
    /// </summary>
    public IReadOnlyList<Node> SupportingSources { get; }
}
