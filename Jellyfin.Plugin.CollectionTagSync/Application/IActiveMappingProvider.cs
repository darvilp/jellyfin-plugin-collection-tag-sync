using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides the currently active validated mapping configuration.
/// </summary>
public interface IActiveMappingProvider
{
    /// <summary>
    /// Gets the active configuration, or <see langword="null"/> when synchronization is not configured.
    /// </summary>
    /// <returns>The active validated configuration.</returns>
    MappingConfiguration? GetConfiguration();
}
