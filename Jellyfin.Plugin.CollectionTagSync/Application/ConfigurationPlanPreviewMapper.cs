using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Maps immutable planner output to non-executable candidate preview diagnostics.
/// </summary>
internal static class ConfigurationPlanPreviewMapper
{
    /// <summary>
    /// Creates one complete candidate configuration preview.
    /// </summary>
    /// <param name="activeRevision">The active configuration revision.</param>
    /// <param name="totalItemCount">The eligible item count.</param>
    /// <param name="plans">Every successfully calculated item plan.</param>
    /// <returns>The serializer-safe preview.</returns>
    public static ConfigurationPlanPreview Create(
        long activeRevision,
        int totalItemCount,
        IEnumerable<ReconciliationPlan> plans)
    {
        return new ConfigurationPlanPreview(
            activeRevision,
            totalItemCount,
            plans.Select(ToPreview));
    }

    private static ConfigurationItemPlanPreview ToPreview(ReconciliationPlan plan)
    {
        return new ConfigurationItemPlanPreview(
            plan.ItemId,
            plan.ItemKind,
            plan.Mutations.Select(mutation => new ConfigurationMutationPreview(
                mutation.Kind,
                MappingNodeConfigurationMapper.FromDomain(mutation.Target),
                mutation.Policy,
                mutation.SupportingSources.Select(MappingNodeConfigurationMapper.FromDomain),
                mutation.TagValues)),
            plan.TargetEvaluations.Select(evaluation => new ConfigurationTargetEvaluationPreview(
                MappingNodeConfigurationMapper.FromDomain(evaluation.Target),
                evaluation.Policy,
                evaluation.ObservedState,
                evaluation.EffectiveState,
                evaluation.SupportingSources.Select(MappingNodeConfigurationMapper.FromDomain))));
    }
}
