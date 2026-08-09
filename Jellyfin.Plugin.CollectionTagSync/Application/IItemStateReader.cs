using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Reads one immutable direct-state snapshot from Jellyfin.
/// </summary>
public interface IItemStateReader
{
    /// <summary>
    /// Reads one eligible item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <param name="configuration">The active validated configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The direct-state snapshot, or <see langword="null"/> for an ineligible or missing item.</returns>
    Task<ObservedItemState?> ReadAsync(
        Guid itemId,
        MappingConfiguration configuration,
        CancellationToken cancellationToken);
}
