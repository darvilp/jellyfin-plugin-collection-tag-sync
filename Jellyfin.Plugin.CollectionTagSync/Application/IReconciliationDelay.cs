using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides cancellable delays for startup and activity-settling policies.
/// </summary>
internal interface IReconciliationDelay
{
    /// <summary>
    /// Delays for the requested duration.
    /// </summary>
    /// <param name="delay">The duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the delay.</returns>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
