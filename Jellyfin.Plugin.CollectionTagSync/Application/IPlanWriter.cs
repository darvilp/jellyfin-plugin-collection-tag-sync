using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Applies planned direct-state mutations through Jellyfin services.
/// </summary>
public interface IPlanWriter
{
    /// <summary>
    /// Applies every mutation in one item plan.
    /// </summary>
    /// <param name="plan">The settled plan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken);
}
