using System;
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
}
