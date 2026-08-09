using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reads, plans, and applies reconciliation for one item.
/// </summary>
public sealed class ItemReconciler
{
    private readonly IActiveMappingProvider _mappingProvider;
    private readonly IItemStateReader _stateReader;
    private readonly IPlanWriter _planWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemReconciler"/> class.
    /// </summary>
    /// <param name="mappingProvider">The active mapping provider.</param>
    /// <param name="stateReader">The direct-state reader.</param>
    /// <param name="planWriter">The direct-state writer.</param>
    public ItemReconciler(
        IActiveMappingProvider mappingProvider,
        IItemStateReader stateReader,
        IPlanWriter planWriter)
    {
        _mappingProvider = mappingProvider;
        _stateReader = stateReader;
        _planWriter = planWriter;
    }

    /// <summary>
    /// Reconciles one item against the current mapping configuration.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan, or <see langword="null"/> when no active mapping or eligible item exists.</returns>
    public async Task<ReconciliationPlan?> ReconcileAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var configuration = _mappingProvider.GetConfiguration();
        if (configuration is null)
        {
            return null;
        }

        return await ReconcileAsync(itemId, configuration, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reconciles one item against an immutable accepted configuration snapshot.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="configuration">The operational configuration snapshot.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The applied or already-settled plan.</returns>
    internal async Task<ReconciliationPlan?> ReconcileAsync(
        Guid itemId,
        MappingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(itemId, configuration, cancellationToken).ConfigureAwait(false);
        if (plan is not null)
        {
            await ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
        }

        return plan;
    }

    /// <summary>
    /// Reads and plans one item without applying mutations.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="configuration">The immutable operational configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete item plan, or <see langword="null"/> when the item is no longer eligible.</returns>
    internal async Task<ReconciliationPlan?> PlanAsync(
        Guid itemId,
        MappingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var observedState = await _stateReader
            .ReadAsync(itemId, configuration, cancellationToken)
            .ConfigureAwait(false);
        if (observedState is null)
        {
            return null;
        }

        return ReconciliationPlanner.Plan(configuration, observedState);
    }

    /// <summary>
    /// Applies one previously calculated item plan with the shared writer.
    /// </summary>
    /// <param name="plan">The complete item plan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the write.</returns>
    internal async Task ApplyAsync(
        ReconciliationPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Mutations.Count > 0)
        {
            await _planWriter.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
        }
    }
}
