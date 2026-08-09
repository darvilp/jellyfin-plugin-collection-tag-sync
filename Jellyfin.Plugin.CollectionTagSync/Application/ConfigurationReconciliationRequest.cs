using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries one accepted configuration reconciliation to the background worker.
/// </summary>
internal sealed class ConfigurationReconciliationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationReconciliationRequest"/> class.
    /// </summary>
    /// <param name="id">The opaque request identity.</param>
    /// <param name="revision">The accepted configuration revision.</param>
    /// <param name="itemIds">The eligible item identities.</param>
    /// <param name="configuration">The immutable accepted configuration.</param>
    /// <param name="precomputedPlans">The exact accepted plans, or <see langword="null"/> for fresh worker planning.</param>
    public ConfigurationReconciliationRequest(
        Guid id,
        long revision,
        IEnumerable<Guid> itemIds,
        MappingConfiguration configuration,
        IEnumerable<ReconciliationPlan>? precomputedPlans = null)
    {
        Id = id;
        Revision = revision;
        ItemIds = Array.AsReadOnly([.. itemIds]);
        Configuration = configuration;
        UsesPrecomputedPlans = precomputedPlans is not null;
        var plansByItem = (precomputedPlans ?? [])
            .ToDictionary(plan => plan.ItemId);
        if (plansByItem.Keys.Except(ItemIds).Any())
        {
            throw new ArgumentException(
                "Every precomputed plan must belong to the request's eligible item set.",
                nameof(precomputedPlans));
        }

        PrecomputedPlans = new ReadOnlyDictionary<Guid, ReconciliationPlan>(plansByItem);
    }

    /// <summary>
    /// Gets the opaque request identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the accepted configuration revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the eligible item identities.
    /// </summary>
    public IReadOnlyList<Guid> ItemIds { get; }

    /// <summary>
    /// Gets the immutable accepted configuration for this revision.
    /// </summary>
    public MappingConfiguration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether execution must apply the accepted exact plans without replanning.
    /// </summary>
    public bool UsesPrecomputedPlans { get; }

    /// <summary>
    /// Gets the exact accepted item plans keyed by item identity.
    /// </summary>
    public IReadOnlyDictionary<Guid, ReconciliationPlan> PrecomputedPlans { get; }
}
