using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Collections;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Applies the bounded Phase 3 collection-addition mutation through Jellyfin.
/// </summary>
internal sealed class JellyfinPlanWriter : IPlanWriter
{
    private readonly ICollectionManager _collectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinPlanWriter"/> class.
    /// </summary>
    /// <param name="collectionManager">The Jellyfin collection manager.</param>
    public JellyfinPlanWriter(ICollectionManager collectionManager)
    {
        _collectionManager = collectionManager;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
    {
        foreach (var mutation in plan.Mutations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mutation.Kind is not PlannedMutationKind.AddCollectionMembership
                || mutation.Target is not CollectionNode collection)
            {
                throw new InvalidOperationException(
                    "The Phase 3 walking-slice writer accepts only collection-membership additions.");
            }

            await _collectionManager
                .AddToCollectionAsync(collection.Id, [plan.ItemId])
                .ConfigureAwait(false);
        }
    }
}
