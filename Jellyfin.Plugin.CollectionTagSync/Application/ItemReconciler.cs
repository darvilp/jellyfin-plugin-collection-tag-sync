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

        var observedState = await _stateReader
            .ReadAsync(itemId, configuration, cancellationToken)
            .ConfigureAwait(false);
        if (observedState is null)
        {
            return null;
        }

        var plan = ReconciliationPlanner.Plan(configuration, observedState);
        if (plan.Mutations.Count > 0)
        {
            await _planWriter.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
        }

        return plan;
    }
}
