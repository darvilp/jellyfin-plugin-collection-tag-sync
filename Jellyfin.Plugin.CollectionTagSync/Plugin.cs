using System;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CollectionTagSync;

/// <summary>
/// The Collection Tag Sync Jellyfin plugin.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
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
}
