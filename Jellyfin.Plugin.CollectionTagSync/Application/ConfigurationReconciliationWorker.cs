using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Settles accepted configuration changes through the shared mutation boundary.
/// </summary>
internal sealed partial class ConfigurationReconciliationWorker : BackgroundService
{
    private readonly ConfigurationReconciliationDispatcher _dispatcher;
    private readonly BackgroundReconciliationStatusStore _statusStore;
    private readonly ItemReconciler _reconciler;
    private readonly IOperationalMappingProvider _operationalMappingProvider;
    private readonly ReconciliationExecutionGate _executionGate;
    private readonly IFailedItemQuarantine _failureQuarantine;
    private readonly ILogger<ConfigurationReconciliationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationReconciliationWorker"/> class.
    /// </summary>
    /// <param name="dispatcher">The accepted request dispatcher.</param>
    /// <param name="statusStore">The privacy-safe status store.</param>
    /// <param name="reconciler">The per-item reconciler.</param>
    /// <param name="operationalMappingProvider">The fail-closed snapshot resolver.</param>
    /// <param name="executionGate">The shared mutation serialization boundary.</param>
    /// <param name="failureQuarantine">The failed-item quarantine.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationReconciliationWorker(
        ConfigurationReconciliationDispatcher dispatcher,
        BackgroundReconciliationStatusStore statusStore,
        ItemReconciler reconciler,
        IOperationalMappingProvider operationalMappingProvider,
        ReconciliationExecutionGate executionGate,
        IFailedItemQuarantine failureQuarantine,
        ILogger<ConfigurationReconciliationWorker> logger)
    {
        _dispatcher = dispatcher;
        _statusStore = statusStore;
        _reconciler = reconciler;
        _operationalMappingProvider = operationalMappingProvider;
        _executionGate = executionGate;
        _failureQuarantine = failureQuarantine;
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One item failure must not abort later items in the accepted background request.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _dispatcher.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var requestStarted = false;
            if (request.ItemIds.Count == 0)
            {
                await _executionGate.EnterAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    _statusStore.MarkRunning(request.Id);
                    requestStarted = true;
                }
                finally
                {
                    _executionGate.Exit();
                }
            }

            foreach (var itemId in request.ItemIds)
            {
                await _executionGate.EnterAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    if (!requestStarted)
                    {
                        _statusStore.MarkRunning(request.Id);
                        requestStarted = true;
                    }

                    var configuration = _operationalMappingProvider.Resolve(request.Configuration);
                    try
                    {
                        await SettleItemAsync(itemId, configuration, stoppingToken).ConfigureAwait(false);
                        _statusStore.RecordSuccess(request.Id);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        _failureQuarantine.Quarantine(itemId);
                        _statusStore.RecordFailure(request.Id);
                        LogItemFailure(_logger, exception, request.Id, itemId);
                    }
                }
                finally
                {
                    _executionGate.Exit();
                }
            }

            _statusStore.MarkFinished(request.Id);
            LogRequestFinished(_logger, request.Id, request.Revision);
        }
    }

    private async Task SettleItemAsync(
        Guid itemId,
        MappingConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var firstPlan = await _reconciler
            .ReconcileAsync(itemId, configuration, cancellationToken)
            .ConfigureAwait(false);
        if (firstPlan is null || firstPlan.Mutations.Count == 0)
        {
            return;
        }

        var settledPlan = await _reconciler
            .ReconcileAsync(itemId, configuration, cancellationToken)
            .ConfigureAwait(false);
        if (settledPlan is not null && settledPlan.Mutations.Count > 0)
        {
            throw new InvalidOperationException("Configuration reconciliation did not settle after applying its plan.");
        }
    }

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Error,
        Message = "Collection Tag Sync background reconciliation failed RequestId={RequestId} ItemId={ItemId}")]
    private static partial void LogItemFailure(ILogger logger, Exception exception, Guid requestId, Guid itemId);

    [LoggerMessage(
        EventId = 31,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync background reconciliation finished RequestId={RequestId} Revision={Revision}")]
    private static partial void LogRequestFinished(ILogger logger, Guid requestId, long revision);
}
