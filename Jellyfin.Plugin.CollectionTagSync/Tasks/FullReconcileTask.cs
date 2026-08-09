using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Tasks;

/// <summary>
/// Exposes canonical whole-library recovery through Jellyfin scheduled tasks.
/// </summary>
public sealed class FullReconcileTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly FullReconcileRequestStore _requestStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileTask"/> class.
    /// </summary>
    /// <param name="requestStore">The coalesced Full Reconcile request store.</param>
    public FullReconcileTask(FullReconcileRequestStore requestStore)
    {
        _requestStore = requestStore;
    }

    /// <inheritdoc />
    public string Name => "Collection Tag Sync: Full Reconcile";

    /// <inheritdoc />
    public string Key => "CollectionTagSyncFullReconcile";

    /// <inheritdoc />
    public string Description => "Repairs Collection Tag Sync drift for every eligible movie and series.";

    /// <inheritdoc />
    public string Category => "Collection Tag Sync";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        progress.Report(0);
        var result = await _requestStore
            .RequestAsync(FullReconcileRequestReason.Manual, cancellationToken)
            .ConfigureAwait(false);
        if (result.State == FullReconcileState.Failed)
        {
            throw new InvalidOperationException("Full Reconcile failed before item-level completion.");
        }

        progress.Report(100);
    }
}
