using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Contains the settled evaluations and direct mutations for one item.
/// </summary>
public sealed class ReconciliationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciliationPlan"/> class.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="itemKind">The eligible item kind.</param>
    /// <param name="targetEvaluations">The settled target evaluations.</param>
    /// <param name="mutations">The required direct mutations.</param>
    internal ReconciliationPlan(
        Guid itemId,
        EligibleItemKind itemKind,
        IEnumerable<TargetEvaluation> targetEvaluations,
        IEnumerable<PlannedMutation> mutations)
    {
        ItemId = itemId;
        ItemKind = itemKind;
        TargetEvaluations = Array.AsReadOnly([.. targetEvaluations]);
        Mutations = Array.AsReadOnly([.. mutations]);
    }

    /// <summary>
    /// Gets the item identifier.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the eligible item kind.
    /// </summary>
    public EligibleItemKind ItemKind { get; }

    /// <summary>
    /// Gets the settled target evaluations.
    /// </summary>
    public IReadOnlyList<TargetEvaluation> TargetEvaluations { get; }

    /// <summary>
    /// Gets the required direct mutations.
    /// </summary>
    public IReadOnlyList<PlannedMutation> Mutations { get; }
}
