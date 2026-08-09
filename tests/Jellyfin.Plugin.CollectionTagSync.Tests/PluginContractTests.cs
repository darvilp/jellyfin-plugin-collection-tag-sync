using System;
using System.IO;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Moq;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.CollectionTagSync.Plugin;

namespace Jellyfin.Plugin.CollectionTagSync.Tests;

public sealed class PluginContractTests
{
    [Fact]
    public void JellyfinIdentifiesPluginByPermanentNameAndId()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Strict);
        applicationPaths.SetupGet(paths => paths.PluginsPath).Returns("/tmp/jellyfin/plugins");

        var plugin = new PluginUnderTest(applicationPaths.Object, Mock.Of<IXmlSerializer>());

        Assert.Equal("Collection Tag Sync", plugin.Name);
        Assert.Equal(new Guid("04920eee-c499-4b13-890f-7af0175f28f0"), plugin.Id);
    }

    [Fact]
    public void GenericConfigurationUpdateCannotBypassValidatedActivation()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Strict);
        applicationPaths.SetupGet(paths => paths.PluginsPath).Returns("/tmp/jellyfin/plugins");
        var plugin = new PluginUnderTest(applicationPaths.Object, Mock.Of<IXmlSerializer>());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            plugin.UpdateConfiguration(new PluginConfiguration()));

        Assert.Contains("activation endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatedSaveMakesAcceptedConfigurationImmediatelyActiveInMemory()
    {
        var initial = new PluginConfiguration { Revision = 2 };
        var accepted = new PluginConfiguration { Revision = 3 };
        var (plugin, serializer) = CreatePluginWithConfiguration(initial);
        serializer
            .Setup(value => value.SerializeToFile(accepted, It.IsAny<string>()));
        var persistence = new PluginConfigurationPersistence();

        _ = plugin.Configuration;
        persistence.Save(accepted);

        Assert.Same(accepted, persistence.Current);
        serializer.Verify(value => value.SerializeToFile(accepted, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void PersistenceFailureLeavesPriorConfigurationActive()
    {
        var initial = new PluginConfiguration { Revision = 2 };
        var accepted = new PluginConfiguration { Revision = 3 };
        var (plugin, serializer) = CreatePluginWithConfiguration(initial);
        serializer
            .Setup(value => value.SerializeToFile(accepted, It.IsAny<string>()))
            .Throws(new IOException("Injected persistence failure."));
        var persistence = new PluginConfigurationPersistence();

        _ = plugin.Configuration;
        Assert.Throws<IOException>(() => persistence.Save(accepted));

        Assert.Same(initial, persistence.Current);
    }

    private static (PluginUnderTest Plugin, Mock<IXmlSerializer> Serializer) CreatePluginWithConfiguration(
        PluginConfiguration initial)
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Strict);
        applicationPaths.SetupGet(paths => paths.PluginsPath).Returns("/tmp/jellyfin/plugins");
        applicationPaths.SetupGet(paths => paths.PluginConfigurationsPath).Returns("/tmp/jellyfin/config");
        var serializer = new Mock<IXmlSerializer>(MockBehavior.Strict);
        serializer
            .Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
            .Returns(initial);
        return (new PluginUnderTest(applicationPaths.Object, serializer.Object), serializer);
    }
}
