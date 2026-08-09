using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Contains one item's direct mutations and final settled target states.
/// </summary>
public sealed class ConfigurationItemPlanPreview
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationItemPlanPreview"/> class.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="itemKind">The eligible item kind.</param>
    /// <param name="mutations">The direct additions and removals.</param>
    /// <param name="targetEvaluations">The final settled target evaluations.</param>
    internal ConfigurationItemPlanPreview(
        Guid itemId,
        EligibleItemKind itemKind,
        IEnumerable<ConfigurationMutationPreview> mutations,
        IEnumerable<ConfigurationTargetEvaluationPreview> targetEvaluations)
    {
        ItemId = itemId;
        ItemKind = itemKind;
        Mutations = [.. mutations];
        TargetEvaluations = [.. targetEvaluations];
    }

    /// <summary>Gets the Jellyfin item identifier.</summary>
    public Guid ItemId { get; }

    /// <summary>Gets the eligible item kind.</summary>
    public EligibleItemKind ItemKind { get; }

    /// <summary>Gets the direct additions and removals.</summary>
    public IReadOnlyList<ConfigurationMutationPreview> Mutations { get; }

    /// <summary>Gets the final settled target evaluations.</summary>
    public IReadOnlyList<ConfigurationTargetEvaluationPreview> TargetEvaluations { get; }
}
