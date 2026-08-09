using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Deduplicates dirty items and serializes their reconciliation.
/// </summary>
public sealed partial class ReconciliationWorker : BackgroundService, IDirtyItemSink, IIncrementalReconciliationControl
{
    private readonly object _sync = new();
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly HashSet<Guid> _pending = [];
    private readonly HashSet<Guid> _running = [];
    private readonly HashSet<Guid> _rerunRequested = [];
    private readonly HashSet<Guid> _quarantined = [];
    private readonly ItemReconciler _reconciler;
    private readonly IncrementalReconciliationOptions _options;
    private readonly FullReconcileRequestStore _fullReconcileRequests;
    private readonly ILogger<ReconciliationWorker> _logger;
    private bool _stormFallbackActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciliationWorker"/> class.
    /// </summary>
    /// <param name="reconciler">The per-item reconciler.</param>
    /// <param name="options">The bounded incremental options.</param>
    /// <param name="fullReconcileRequests">The coalesced Full Reconcile request store.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliationWorker(
        ItemReconciler reconciler,
        IncrementalReconciliationOptions options,
        FullReconcileRequestStore fullReconcileRequests,
        ILogger<ReconciliationWorker> logger)
    {
        _reconciler = reconciler;
        _options = options;
        _fullReconcileRequests = fullReconcileRequests;
        _logger = logger;
    }

    /// <summary>
    /// Gets a privacy-safe snapshot of queued, running, and quarantined work.
    /// </summary>
    public IncrementalReconciliationStatus Status
    {
        get
        {
            lock (_sync)
            {
                return new IncrementalReconciliationStatus(
                    _pending.Count,
                    _running.Count,
                    _quarantined.Count,
                    _stormFallbackActive);
            }
        }
    }

    /// <inheritdoc />
    public void MarkDirty(Guid itemId)
    {
        lock (_sync)
        {
            if (_quarantined.Contains(itemId) || _stormFallbackActive)
            {
                return;
            }

            if (_pending.Contains(itemId))
            {
                return;
            }

            if (_running.Contains(itemId))
            {
                _rerunRequested.Add(itemId);
                return;
            }

            if (_pending.Count >= _options.MaxPendingItems)
            {
                _stormFallbackActive = true;
                _fullReconcileRequests.Request(FullReconcileRequestReason.EventStorm);
                LogEventStorm(_logger, _options.MaxPendingItems);
                return;
            }

            _pending.Add(itemId);
            _queue.Writer.TryWrite(itemId);
        }
    }

    /// <inheritdoc />
    public void ResetAfterFullReconcile()
    {
        lock (_sync)
        {
            _quarantined.Clear();
            _stormFallbackActive = false;
            _fullReconcileRequests.Clear();
        }
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One item failure must not terminate reconciliation for every later item.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var itemId in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            BeginItem(itemId);
            try
            {
                var plan = await _reconciler.ReconcileAsync(itemId, stoppingToken).ConfigureAwait(false);
                if (plan is null)
                {
                    continue;
                }

                if (plan.Mutations.Count == 0)
                {
                    LogSettled(_logger, itemId);
                }
                else
                {
                    LogApplied(_logger, itemId, plan.Mutations.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                QuarantineItem(itemId);
                LogFailure(_logger, exception, itemId);
            }
            finally
            {
                FinishItem(itemId);
            }
        }
    }

    private void BeginItem(Guid itemId)
    {
        lock (_sync)
        {
            _pending.Remove(itemId);
            _running.Add(itemId);
        }
    }

    private void FinishItem(Guid itemId)
    {
        lock (_sync)
        {
            _running.Remove(itemId);
            if (_quarantined.Contains(itemId) || _stormFallbackActive)
            {
                _rerunRequested.Remove(itemId);
                return;
            }

            if (_rerunRequested.Remove(itemId))
            {
                _pending.Add(itemId);
                _queue.Writer.TryWrite(itemId);
            }
        }
    }

    private void QuarantineItem(Guid itemId)
    {
        lock (_sync)
        {
            _quarantined.Add(itemId);
            _rerunRequested.Remove(itemId);
        }
    }

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync reconciliation applied ItemId={ItemId} MutationCount={MutationCount}")]
    private static partial void LogApplied(ILogger logger, Guid itemId, int mutationCount);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync reconciliation settled ItemId={ItemId} MutationCount=0")]
    private static partial void LogSettled(ILogger logger, Guid itemId);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Collection Tag Sync reconciliation failed ItemId={ItemId}")]
    private static partial void LogFailure(ILogger logger, Exception exception, Guid itemId);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Warning,
        Message = "Collection Tag Sync event storm exceeded MaxPendingItems={MaxPendingItems}; Full Reconcile requested")]
    private static partial void LogEventStorm(ILogger logger, int maxPendingItems);
}
