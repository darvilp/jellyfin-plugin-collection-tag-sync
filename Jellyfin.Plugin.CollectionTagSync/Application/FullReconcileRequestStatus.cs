using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides a privacy-safe snapshot of coalesced Full Reconcile demand.
/// </summary>
public sealed class FullReconcileRequestStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileRequestStatus"/> class.
    /// </summary>
    /// <param name="reasons">The coalesced request reasons.</param>
    internal FullReconcileRequestStatus(IEnumerable<FullReconcileRequestReason> reasons)
    {
        Reasons = Array.AsReadOnly([.. reasons]);
    }

    /// <summary>
    /// Gets a value indicating whether Full Reconcile is required.
    /// </summary>
    public bool IsRequested => Reasons.Count > 0;

    /// <summary>
    /// Gets the coalesced reasons without exposing library or item identities.
    /// </summary>
    public IReadOnlyList<FullReconcileRequestReason> Reasons { get; }
}
