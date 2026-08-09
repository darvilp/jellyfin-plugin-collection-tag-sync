using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Calculates and applies serialized whole-library recovery runs.
/// </summary>
internal sealed partial class FullReconciliationWorker : BackgroundService
{
    private readonly FullReconcileRequestStore _requestStore;
    private readonly FullReconcileStatusStore _statusStore;
    private readonly IConfigurationCatalog _catalog;
    private readonly IActiveMappingProvider _mappingProvider;
    private readonly ItemReconciler _reconciler;
    private readonly ReconciliationExecutionGate _executionGate;
    private readonly IIncrementalReconciliationControl _incrementalControl;
    private readonly IReconciliationActivityMonitor _activityMonitor;
    private readonly ILogger<FullReconciliationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconciliationWorker"/> class.
    /// </summary>
    /// <param name="requestStore">The coalesced request store.</param>
    /// <param name="statusStore">The privacy-safe status store.</param>
    /// <param name="catalog">The eligible item catalog.</param>
    /// <param name="mappingProvider">The active operational mapping provider.</param>
    /// <param name="reconciler">The shared item planner and writer.</param>
    /// <param name="executionGate">The process-wide mutation gate.</param>
    /// <param name="incrementalControl">The incremental recovery boundary.</param>
    /// <param name="activityMonitor">The scan and event-activity monitor.</param>
    /// <param name="logger">The logger.</param>
    public FullReconciliationWorker(
        FullReconcileRequestStore requestStore,
        FullReconcileStatusStore statusStore,
        IConfigurationCatalog catalog,
        IActiveMappingProvider mappingProvider,
        ItemReconciler reconciler,
        ReconciliationExecutionGate executionGate,
        IIncrementalReconciliationControl incrementalControl,
        IReconciliationActivityMonitor activityMonitor,
        ILogger<FullReconciliationWorker> logger)
    {
        _requestStore = requestStore;
        _statusStore = statusStore;
        _catalog = catalog;
        _mappingProvider = mappingProvider;
        _reconciler = reconciler;
        _executionGate = executionGate;
        _incrementalControl = incrementalControl;
        _activityMonitor = activityMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A run-wide fault must complete waiters and leave the background coordinator available for a later request.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var unused in _requestStore.SignalReader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            _ = unused;
            var waitedForQuiet = RequiresQuietServer(_requestStore.Status.Reasons);
            if (waitedForQuiet)
            {
                await _activityMonitor.WaitUntilQuietAsync(stoppingToken).ConfigureAwait(false);
            }

            if (!_requestStore.TryClaim(out var request))
            {
                continue;
            }

            if (!waitedForQuiet && RequiresQuietServer(request.Reasons))
            {
                await _activityMonitor.WaitUntilQuietAsync(stoppingToken).ConfigureAwait(false);
            }

            try
            {
                var result = await RunAsync(request, stoppingToken).ConfigureAwait(false);
                request.Complete(result);
            }
            catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
            {
                CompleteRunWideFailure(request, exception);
                return;
            }
            catch (Exception exception)
            {
                CompleteRunWideFailure(request, exception);
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A Full Reconcile must contain one item failure and continue with later items.")]
    private async Task<FullReconcileRunResult> RunAsync(
        FullReconcileRequest request,
        CancellationToken cancellationToken)
    {
        await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = _mappingProvider.GetConfiguration();
            var itemIds = configuration is null
                ? []
                : _catalog.GetEligibleItemIds().Distinct().ToArray();
            _statusStore.Update(new FullReconcileRunResult(
                request.Id,
                FullReconcileState.Planning,
                request.Reasons,
                itemIds.Length,
                succeededItemCount: 0,
                failedItemCount: 0));

            var plans = new List<(Guid ItemId, ReconciliationPlan? Plan)>(itemIds.Length);
            var failedItemIds = new HashSet<Guid>();
            foreach (var itemId in itemIds)
            {
                try
                {
                    var plan = await _reconciler
                        .PlanAsync(itemId, configuration!, cancellationToken)
                        .ConfigureAwait(false);
                    plans.Add((itemId, plan));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failedItemIds.Add(itemId);
                    LogItemFailure(
                        _logger,
                        exception,
                        request.Id,
                        itemId,
                        FullReconcileState.Planning);
                }
            }

            _statusStore.Update(new FullReconcileRunResult(
                request.Id,
                FullReconcileState.Applying,
                request.Reasons,
                itemIds.Length,
                succeededItemCount: 0,
                failedItemIds.Count));
            var repairedItemIds = new List<Guid>(plans.Count);
            foreach (var (itemId, plan) in plans)
            {
                try
                {
                    if (plan is not null)
                    {
                        await _reconciler.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
                    }

                    repairedItemIds.Add(itemId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failedItemIds.Add(itemId);
                    LogItemFailure(
                        _logger,
                        exception,
                        request.Id,
                        itemId,
                        FullReconcileState.Applying);
                }
            }

            _incrementalControl.CompleteFullReconcile(repairedItemIds, failedItemIds);
            var state = failedItemIds.Count == 0
                ? FullReconcileState.Completed
                : FullReconcileState.CompletedWithFailures;
            var result = new FullReconcileRunResult(
                request.Id,
                state,
                request.Reasons,
                itemIds.Length,
                repairedItemIds.Count,
                failedItemIds.Count);
            _statusStore.Update(result);
            LogRunFinished(
                _logger,
                result.Id,
                result.TotalItemCount,
                result.SucceededItemCount,
                result.FailedItemCount);
            return result;
        }
        finally
        {
            _executionGate.Exit();
        }
    }

    private static bool RequiresQuietServer(IEnumerable<FullReconcileRequestReason> reasons)
    {
        return reasons.Any(reason => reason is
            FullReconcileRequestReason.Startup or FullReconcileRequestReason.EventStorm);
    }

    private void CompleteRunWideFailure(FullReconcileRequest request, Exception exception)
    {
        var result = new FullReconcileRunResult(
            request.Id,
            FullReconcileState.Failed,
            request.Reasons,
            totalItemCount: 0,
            succeededItemCount: 0,
            failedItemCount: 0);
        _statusStore.Update(result);
        request.Complete(result);
        LogRunFailure(_logger, exception, request.Id);
    }

    [LoggerMessage(
        EventId = 40,
        Level = LogLevel.Error,
        Message = "Collection Tag Sync Full Reconcile item failed RunId={RunId} ItemId={ItemId} Phase={Phase}")]
    private static partial void LogItemFailure(
        ILogger logger,
        Exception exception,
        Guid runId,
        Guid itemId,
        FullReconcileState phase);

    [LoggerMessage(
        EventId = 41,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync Full Reconcile finished RunId={RunId} Total={TotalItemCount} Succeeded={SucceededItemCount} Failed={FailedItemCount}")]
    private static partial void LogRunFinished(
        ILogger logger,
        Guid runId,
        int totalItemCount,
        int succeededItemCount,
        int failedItemCount);

    [LoggerMessage(
        EventId = 42,
        Level = LogLevel.Error,
        Message = "Collection Tag Sync Full Reconcile failed before item-level completion RunId={RunId}")]
    private static partial void LogRunFailure(ILogger logger, Exception exception, Guid runId);
}
