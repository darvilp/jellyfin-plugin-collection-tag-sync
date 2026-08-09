using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CollectionTagSync;

/// <summary>
/// The Collection Tag Sync Jellyfin plugin.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The Jellyfin application paths.</param>
    /// <param name="xmlSerializer">The Jellyfin XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the loaded plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; } = null!;

    /// <inheritdoc />
    public override string Name => "Collection Tag Sync";

    /// <inheritdoc />
    public override string Description =>
        "Synchronizes direct Movie and Series tags and collection memberships through explicit mappings.";

    /// <inheritdoc />
    public override Guid Id => new("04920eee-c499-4b13-890f-7af0175f28f0");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        const string resourcePrefix = "Jellyfin.Plugin.CollectionTagSync.Configuration";
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = resourcePrefix + ".configPage.html",
        };
        yield return new PluginPageInfo
        {
            Name = Name + ".js",
            EmbeddedResourcePath = resourcePrefix + ".configPage.js",
        };
    }

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        throw new InvalidOperationException(
            "Collection Tag Sync configuration must use the validated activation endpoint.");
    }

    /// <summary>
    /// Persists and activates one server-validated configuration.
    /// </summary>
    /// <param name="configuration">The validated configuration.</param>
    internal void ActivateValidatedConfiguration(PluginConfiguration configuration)
    {
        SaveConfiguration(configuration);
        Configuration = configuration;
        ConfigurationChanged?.Invoke(this, configuration);
    }
}
