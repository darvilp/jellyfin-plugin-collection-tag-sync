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
public sealed partial class ReconciliationWorker : BackgroundService, IDirtyItemSink
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
    private readonly ItemReconciler _reconciler;
    private readonly ILogger<ReconciliationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconciliationWorker"/> class.
    /// </summary>
    /// <param name="reconciler">The per-item reconciler.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliationWorker(
        ItemReconciler reconciler,
        ILogger<ReconciliationWorker> logger)
    {
        _reconciler = reconciler;
        _logger = logger;
    }

    /// <inheritdoc />
    public void MarkDirty(Guid itemId)
    {
        lock (_sync)
        {
            if (_pending.Contains(itemId))
            {
                return;
            }

            if (_running.Contains(itemId))
            {
                _rerunRequested.Add(itemId);
                return;
            }

            _pending.Add(itemId);
            _queue.Writer.TryWrite(itemId);
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
            if (_rerunRequested.Remove(itemId))
            {
                _pending.Add(itemId);
                _queue.Writer.TryWrite(itemId);
            }
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
}
