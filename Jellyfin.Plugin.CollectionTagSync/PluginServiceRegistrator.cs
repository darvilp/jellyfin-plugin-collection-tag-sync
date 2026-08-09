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
        serviceCollection.AddSingleton<IActiveMappingProvider, WalkingSliceMappingProvider>();
        serviceCollection.AddSingleton<IItemStateReader, JellyfinItemStateReader>();
        serviceCollection.AddSingleton<IPlanWriter, JellyfinPlanWriter>();
        serviceCollection.AddSingleton<ItemReconciler>();
        serviceCollection.AddSingleton<ReconciliationWorker>();
        serviceCollection.AddSingleton<IDirtyItemSink>(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddHostedService(
            serviceProvider => serviceProvider.GetRequiredService<ReconciliationWorker>());
        serviceCollection.AddHostedService<JellyfinEventObserver>();
    }
}
