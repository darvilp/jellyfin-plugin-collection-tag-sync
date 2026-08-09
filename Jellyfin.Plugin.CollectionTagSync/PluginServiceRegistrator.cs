using System;
using Jellyfin.Plugin.CollectionTagSync.Application;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CollectionTagSync;

/// <summary>
/// Registers Collection Tag Sync services with Jellyfin.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<MappingDiagnosticStore>();
        serviceCollection.AddSingleton<PluginMappingProvider>();
        serviceCollection.AddSingleton<IActiveMappingProvider>(
            serviceProvider => serviceProvider.GetRequiredService<PluginMappingProvider>());
        serviceCollection.AddSingleton<IOperationalMappingProvider>(
            serviceProvider => serviceProvider.GetRequiredService<PluginMappingProvider>());
        serviceCollection.AddSingleton<IItemStateReader, JellyfinItemStateReader>();
        serviceCollection.AddSingleton<IPlanWriter, JellyfinPlanWriter>();
        serviceCollection.AddSingleton<IPluginConfigurationPersistence, PluginConfigurationPersistence>();
        serviceCollection.AddSingleton<IConfigurationCatalog, JellyfinConfigurationCatalog>();
        serviceCollection.AddSingleton<BackgroundReconciliationStatusStore>();
        serviceCollection.AddSingleton<ConfigurationReconciliationDispatcher>();
        serviceCollection.AddSingleton<ConfigurationActivationService>();
        serviceCollection.AddSingleton<ItemReconciler>();
        serviceCollection.AddSingleton<IncrementalReconciliationOptions>();
        serviceCollection.AddSingleton<FullReconcileRequestStore>();
        serviceCollection.AddSingleton<FullReconcileStatusStore>();
        serviceCollection.AddSingleton<FullReconcileSafetyService>();
        serviceCollection.AddSingleton<FullReconcileApprovalService>();
        serviceCollection.AddSingleton<ReconciliationExecutionGate>();
        serviceCollection.AddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IReconciliationDelay, SystemReconciliationDelay>();
        serviceCollection.AddSingleton<IReconciliationActivityMonitor, ReconciliationActivityMonitor>();
        serviceCollection.AddHostedService<MappingDiagnosticInitializer>();
        serviceCollection.AddSingleton<ReconciliationWorker>();
        serviceCollection.AddSingleton<IDirtyItemSink>(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddSingleton<IIncrementalReconciliationControl>(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddSingleton<IFailedItemQuarantine>(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddHostedService(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddHostedService<ConfigurationReconciliationWorker>();
        serviceCollection.AddHostedService<FullReconciliationWorker>();
        serviceCollection.AddHostedService<JellyfinEventObserver>();
        serviceCollection.AddHostedService<StartupReconciliationWorker>();
    }
}
