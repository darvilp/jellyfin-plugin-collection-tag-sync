using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Defines bounded incremental reconciliation behavior.
/// </summary>
public sealed class IncrementalReconciliationOptions
{
    /// <summary>
    /// The default maximum number of unique pending item identities.
    /// </summary>
    public const int DefaultMaxPendingItems = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalReconciliationOptions"/> class.
    /// </summary>
    /// <param name="maxPendingItems">The maximum unique pending item identities.</param>
    public IncrementalReconciliationOptions(int maxPendingItems = DefaultMaxPendingItems)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPendingItems, 1);
        MaxPendingItems = maxPendingItems;
    }

    /// <summary>
    /// Gets the maximum unique pending item identities before storm fallback.
    /// </summary>
    public int MaxPendingItems { get; }
}
