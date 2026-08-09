using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Tracks relevant library activity and waits until scans and event bursts settle.
/// </summary>
internal interface IReconciliationActivityMonitor
{
    /// <summary>Records one eligible library event.</summary>
    void RecordActivity();

    /// <summary>
    /// Waits until no library scan or recent eligible event remains active.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the wait.</returns>
    Task WaitUntilQuietAsync(CancellationToken cancellationToken);
}
