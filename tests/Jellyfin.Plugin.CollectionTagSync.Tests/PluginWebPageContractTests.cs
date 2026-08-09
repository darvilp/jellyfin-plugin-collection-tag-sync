using System;
using System.IO;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Moq;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.CollectionTagSync.Plugin;

namespace Jellyfin.Plugin.CollectionTagSync.Tests;

public sealed class PluginWebPageContractTests
{
    [Fact]
    public void PluginPublishesEmbeddedAdministratorPageAndController()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Strict);
        applicationPaths.SetupGet(paths => paths.PluginsPath).Returns("/tmp/jellyfin/plugins");
        var plugin = new PluginUnderTest(applicationPaths.Object, Mock.Of<IXmlSerializer>());

        var pages = Assert.IsAssignableFrom<IHasWebPages>(plugin).GetPages().ToArray();

        Assert.Collection(
            pages,
            page =>
            {
                Assert.Equal("Collection Tag Sync", page.Name);
                Assert.Equal(
                    "Jellyfin.Plugin.CollectionTagSync.Configuration.configPage.html",
                    page.EmbeddedResourcePath);
            },
            page =>
            {
                Assert.Equal("Collection Tag Sync.js", page.Name);
                Assert.Equal(
                    "Jellyfin.Plugin.CollectionTagSync.Configuration.configPage.js",
                    page.EmbeddedResourcePath);
            });
    }

    [Fact]
    public void AdministratorPageLoadsThinControllerAndExposesAccessibleOperations()
    {
        var assembly = typeof(PluginUnderTest).Assembly;
        using var resource = assembly.GetManifestResourceStream(
            "Jellyfin.Plugin.CollectionTagSync.Configuration.configPage.html");

        Assert.NotNull(resource);
        using var reader = new StreamReader(resource);
        var html = reader.ReadToEnd();

        Assert.Contains("data-controller=\"__plugin/Collection Tag Sync.js\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"collectionTagSyncMappings\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"collectionTagSyncRunOnce\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"collectionTagSyncFullReconcile\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
    }
}
