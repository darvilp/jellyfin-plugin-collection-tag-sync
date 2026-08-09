using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides the single process-wide synchronization mutation boundary.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The singleton gate lives for the Jellyfin process lifetime.")]
public sealed class ReconciliationExecutionGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Waits to enter the exclusive reconciliation boundary.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing boundary acquisition.</returns>
    public Task EnterAsync(CancellationToken cancellationToken)
    {
        return _semaphore.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Releases the exclusive reconciliation boundary.
    /// </summary>
    public void Exit()
    {
        _semaphore.Release();
    }
}
