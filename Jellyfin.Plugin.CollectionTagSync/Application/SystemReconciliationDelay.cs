using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Uses the system clock for production reconciliation delays.
/// </summary>
internal sealed class SystemReconciliationDelay : IReconciliationDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
