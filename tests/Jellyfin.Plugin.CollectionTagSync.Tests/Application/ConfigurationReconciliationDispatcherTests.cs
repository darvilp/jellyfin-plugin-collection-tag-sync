using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class ConfigurationReconciliationDispatcherTests
{
    [Fact]
    public async Task QueuedRequestsRetainTheirAcceptedConfigurationSnapshots()
    {
        var statusStore = new BackgroundReconciliationStatusStore();
        var dispatcher = new ConfigurationReconciliationDispatcher(statusStore);
        var firstConfiguration = CreateConfiguration("First");
        var secondConfiguration = CreateConfiguration("Second");

        _ = dispatcher.Enqueue(revision: 4, [], firstConfiguration);
        _ = dispatcher.Enqueue(revision: 5, [], secondConfiguration);
        var firstRequest = await dispatcher.Reader.ReadAsync().ConfigureAwait(true);
        var secondRequest = await dispatcher.Reader.ReadAsync().ConfigureAwait(true);

        Assert.Equal(4, firstRequest.Revision);
        Assert.Same(firstConfiguration, firstRequest.Configuration);
        Assert.Equal(5, secondRequest.Revision);
        Assert.Same(secondConfiguration, secondRequest.Configuration);
    }

    private static MappingConfiguration CreateConfiguration(string target)
    {
        return Assert.IsType<MappingConfiguration>(MappingConfiguration.Create(
            [
                new MappingGroupDefinition(
                    new TagNodeDefinition(target),
                    [new TagNodeDefinition("Source")],
                    MappingPolicy.Additive,
                    isEnabled: true),
            ]).Configuration);
    }
}
