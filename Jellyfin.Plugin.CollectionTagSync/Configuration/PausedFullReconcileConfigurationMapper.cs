using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Maps immutable planner output to and from non-executable persisted preview diagnostics.
/// </summary>
internal static class PausedFullReconcileConfigurationMapper
{
    /// <summary>
    /// Creates complete persisted diagnostics from one freshly calculated bulk plan.
    /// </summary>
    /// <param name="runId">The opaque run identity.</param>
    /// <param name="configurationRevision">The active configuration revision.</param>
    /// <param name="createdUtc">The UTC diagnostic creation time.</param>
    /// <param name="reasons">The coalesced run reasons.</param>
    /// <param name="totalItemCount">The total eligible-item count.</param>
    /// <param name="plans">Every successfully calculated item plan.</param>
    /// <param name="evaluation">The destructive safety evaluation.</param>
    /// <returns>Serializer-friendly non-executable preview diagnostics.</returns>
    public static PausedFullReconcileConfiguration Create(
        Guid runId,
        long configurationRevision,
        DateTime createdUtc,
        IEnumerable<FullReconcileRequestReason> reasons,
        int totalItemCount,
        IEnumerable<ReconciliationPlan> plans,
        DestructiveCircuitBreakerResult evaluation)
    {
        return new PausedFullReconcileConfiguration
        {
            RunId = runId,
            ConfigurationRevision = configurationRevision,
            CreatedUtc = createdUtc,
            Reasons = [.. reasons],
            TotalItemCount = totalItemCount,
            UniqueAffectedItemCount = evaluation.UniqueAffectedItemCount,
            ExceedsAbsoluteLimit = evaluation.ExceedsAbsoluteLimit,
            Removals = evaluation.Removals.Select(ToConfiguration).ToArray(),
            Groups = evaluation.Groups.Select(ToConfiguration).ToArray(),
            Items = plans.Select(ToConfiguration).ToArray(),
        };
    }

    /// <summary>
    /// Clones persisted diagnostics for safe API exposure or configuration snapshots.
    /// </summary>
    /// <param name="paused">The source diagnostics.</param>
    /// <returns>An independent clone, or <see langword="null"/>.</returns>
    public static PausedFullReconcileConfiguration? Clone(PausedFullReconcileConfiguration? paused)
    {
        if (paused is null)
        {
            return null;
        }

        return new PausedFullReconcileConfiguration
        {
            RunId = paused.RunId,
            ConfigurationRevision = paused.ConfigurationRevision,
            CreatedUtc = paused.CreatedUtc,
            Reasons = [.. paused.Reasons ?? []],
            TotalItemCount = paused.TotalItemCount,
            UniqueAffectedItemCount = paused.UniqueAffectedItemCount,
            ExceedsAbsoluteLimit = paused.ExceedsAbsoluteLimit,
            Removals = (paused.Removals ?? []).Select(removal =>
                new PausedFullReconcileRemovalConfiguration
                {
                    ItemId = removal.ItemId,
                    Target = MappingNodeConfigurationMapper.Clone(removal.Target),
                    Kind = removal.Kind,
                }).ToArray(),
            Groups = (paused.Groups ?? []).Select(group =>
                new PausedFullReconcileGroupConfiguration
                {
                    Target = MappingNodeConfigurationMapper.Clone(group.Target),
                    CurrentAssignmentCount = group.CurrentAssignmentCount,
                    RemovalCount = group.RemovalCount,
                    ExceedsPercentageLimit = group.ExceedsPercentageLimit,
                }).ToArray(),
            Items = (paused.Items ?? []).Select(item =>
                new PausedFullReconcileItemConfiguration
                {
                    ItemId = item.ItemId,
                    ItemKind = item.ItemKind,
                    Mutations = (item.Mutations ?? []).Select(mutation =>
                        new PausedFullReconcileMutationConfiguration
                        {
                            Kind = mutation.Kind,
                            Target = MappingNodeConfigurationMapper.Clone(mutation.Target),
                            Policy = mutation.Policy,
                            SupportingSources = (mutation.SupportingSources ?? [])
                                .Select(MappingNodeConfigurationMapper.Clone)
                                .ToArray(),
                            TagValues = [.. mutation.TagValues ?? []],
                        }).ToArray(),
                    TargetEvaluations = (item.TargetEvaluations ?? []).Select(evaluation =>
                        new PausedFullReconcileTargetEvaluationConfiguration
                        {
                            Target = MappingNodeConfigurationMapper.Clone(evaluation.Target),
                            Policy = evaluation.Policy,
                            ObservedState = evaluation.ObservedState,
                            EffectiveState = evaluation.EffectiveState,
                            SupportingSources = (evaluation.SupportingSources ?? [])
                                .Select(MappingNodeConfigurationMapper.Clone)
                                .ToArray(),
                        }).ToArray(),
                }).ToArray(),
        };
    }

    /// <summary>
    /// Restores the normalized removal tuples used only for fresh-plan equivalence checking.
    /// </summary>
    /// <param name="paused">The persisted diagnostics.</param>
    /// <returns>The exact normalized removal set.</returns>
    public static IReadOnlyList<DestructiveRemoval> ToRemovals(PausedFullReconcileConfiguration paused)
    {
        return Array.AsReadOnly((paused.Removals ?? []).Select(ToDomain).ToArray());
    }

    private static PausedFullReconcileRemovalConfiguration ToConfiguration(DestructiveRemoval removal)
    {
        return new PausedFullReconcileRemovalConfiguration
        {
            ItemId = removal.ItemId,
            Target = MappingNodeConfigurationMapper.FromDomain(removal.Target),
            Kind = removal.Kind,
        };
    }

    private static PausedFullReconcileGroupConfiguration ToConfiguration(DestructiveGroupEvaluation group)
    {
        return new PausedFullReconcileGroupConfiguration
        {
            Target = MappingNodeConfigurationMapper.FromDomain(group.Target),
            CurrentAssignmentCount = group.CurrentAssignmentCount,
            RemovalCount = group.RemovalCount,
            ExceedsPercentageLimit = group.ExceedsPercentageLimit,
        };
    }

    private static PausedFullReconcileItemConfiguration ToConfiguration(ReconciliationPlan plan)
    {
        return new PausedFullReconcileItemConfiguration
        {
            ItemId = plan.ItemId,
            ItemKind = plan.ItemKind,
            Mutations = plan.Mutations.Select(mutation =>
                new PausedFullReconcileMutationConfiguration
                {
                    Kind = mutation.Kind,
                    Target = MappingNodeConfigurationMapper.FromDomain(mutation.Target),
                    Policy = mutation.Policy,
                    SupportingSources = mutation.SupportingSources
                        .Select(MappingNodeConfigurationMapper.FromDomain)
                        .ToArray(),
                    TagValues = [.. mutation.TagValues],
                }).ToArray(),
            TargetEvaluations = plan.TargetEvaluations.Select(evaluation =>
                new PausedFullReconcileTargetEvaluationConfiguration
                {
                    Target = MappingNodeConfigurationMapper.FromDomain(evaluation.Target),
                    Policy = evaluation.Policy,
                    ObservedState = evaluation.ObservedState,
                    EffectiveState = evaluation.EffectiveState,
                    SupportingSources = evaluation.SupportingSources
                        .Select(MappingNodeConfigurationMapper.FromDomain)
                        .ToArray(),
                }).ToArray(),
        };
    }

    private static DestructiveRemoval ToDomain(PausedFullReconcileRemovalConfiguration removal)
    {
        Node target = removal.Target.Kind switch
        {
            MappingNodeKind.Tag => new TagNode(removal.Target.TagValue.Trim()),
            MappingNodeKind.Collection => new CollectionNode(
                removal.Target.CollectionId,
                removal.Target.CollectionDisplayName),
            _ => throw new InvalidOperationException("Unknown persisted paused target type."),
        };
        return new DestructiveRemoval(removal.ItemId, target, removal.Kind);
    }
}
