using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Queues one delayed startup recovery request when continuous mappings are enabled.
/// </summary>
internal sealed partial class StartupReconciliationWorker : BackgroundService
{
    /// <summary>
    /// The polling interval used only while Jellyfin core startup is incomplete.
    /// </summary>
    internal static readonly TimeSpan ServerReadyPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IServerApplicationHost _applicationHost;
    private readonly IPluginConfigurationPersistence _configurationPersistence;
    private readonly IActiveMappingProvider _mappingProvider;
    private readonly FullReconcileRequestStore _requestStore;
    private readonly IReconciliationDelay _delay;
    private readonly ILogger<StartupReconciliationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupReconciliationWorker"/> class.
    /// </summary>
    /// <param name="applicationHost">The Jellyfin application host.</param>
    /// <param name="configurationPersistence">The active configuration source.</param>
    /// <param name="mappingProvider">The active mapping provider.</param>
    /// <param name="requestStore">The coalesced Full Reconcile request store.</param>
    /// <param name="delay">The cancellable delay boundary.</param>
    /// <param name="logger">The logger.</param>
    public StartupReconciliationWorker(
        IServerApplicationHost applicationHost,
        IPluginConfigurationPersistence configurationPersistence,
        IActiveMappingProvider mappingProvider,
        FullReconcileRequestStore requestStore,
        IReconciliationDelay delay,
        ILogger<StartupReconciliationWorker> logger)
    {
        _applicationHost = applicationHost;
        _configurationPersistence = configurationPersistence;
        _mappingProvider = mappingProvider;
        _requestStore = requestStore;
        _delay = delay;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_applicationHost.CoreStartupHasCompleted)
        {
            await _delay.DelayAsync(ServerReadyPollInterval, stoppingToken).ConfigureAwait(false);
        }

        if (_mappingProvider.GetConfiguration() is null)
        {
            return;
        }

        var configuredDelay = _configurationPersistence.Current.StartupReconcileDelayMinutes;
        var delayMinutes = StartupReconcileOptions.IsValidDelay(configuredDelay)
            ? configuredDelay
            : StartupReconcileOptions.DefaultDelayMinutes;
        if (delayMinutes != configuredDelay)
        {
            LogInvalidDelayFallback(_logger, configuredDelay, delayMinutes);
        }

        await _delay
            .DelayAsync(TimeSpan.FromMinutes(delayMinutes), stoppingToken)
            .ConfigureAwait(false);
        _requestStore.Request(FullReconcileRequestReason.Startup);
        LogStartupRequested(_logger, delayMinutes);
    }

    [LoggerMessage(
        EventId = 50,
        Level = LogLevel.Warning,
        Message = "Collection Tag Sync ignored invalid persisted StartupReconcileDelayMinutes={ConfiguredDelayMinutes}; using {FallbackDelayMinutes}")]
    private static partial void LogInvalidDelayFallback(
        ILogger logger,
        int configuredDelayMinutes,
        int fallbackDelayMinutes);

    [LoggerMessage(
        EventId = 51,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync queued startup Full Reconcile after DelayMinutes={DelayMinutes}")]
    private static partial void LogStartupRequested(ILogger logger, int delayMinutes);
}
