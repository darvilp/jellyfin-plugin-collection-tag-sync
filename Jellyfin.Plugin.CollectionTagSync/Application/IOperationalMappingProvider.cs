using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Resolves a validated configuration against current external references.
/// </summary>
internal interface IOperationalMappingProvider
{
    /// <summary>
    /// Resolves one immutable accepted configuration for execution.
    /// </summary>
    /// <param name="configuration">The accepted configuration snapshot.</param>
    /// <returns>The current fail-closed operational configuration.</returns>
    MappingConfiguration Resolve(MappingConfiguration configuration);
}
