using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides the validated, fail-closed operational graph from plugin configuration.
/// </summary>
internal sealed partial class PluginMappingProvider : IActiveMappingProvider, IOperationalMappingProvider
{
    private readonly ILibraryManager _libraryManager;
    private readonly MappingDiagnosticStore _diagnosticStore;
    private readonly ILogger<PluginMappingProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMappingProvider"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="diagnosticStore">The operational diagnostic store.</param>
    /// <param name="logger">The logger.</param>
    public PluginMappingProvider(
        ILibraryManager libraryManager,
        MappingDiagnosticStore diagnosticStore,
        ILogger<PluginMappingProvider> logger)
    {
        _libraryManager = libraryManager;
        _diagnosticStore = diagnosticStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public MappingConfiguration? GetConfiguration()
    {
        var validated = PluginConfigurationMapper.ToDomain(Plugin.Instance.Configuration).Configuration;
        if (validated is null || !validated.Groups.Any(group => group.IsEnabled))
        {
            UpdateDiagnostics([]);
            return null;
        }

        return Resolve(validated);
    }

    /// <inheritdoc />
    public MappingConfiguration Resolve(MappingConfiguration configuration)
    {
        var availableCollectionIds = configuration.Groups
            .Where(group => group.IsEnabled)
            .SelectMany(group => group.Sources.Append(group.Target))
            .OfType<CollectionNode>()
            .Distinct()
            .Where(collection => _libraryManager.GetItemById(collection.Id) is BoxSet)
            .Select(collection => collection.Id);
        var operational = OperationalMappingResolver.Resolve(configuration, availableCollectionIds);
        UpdateDiagnostics(operational.UnresolvedGroups);
        return operational.Configuration;
    }

    private void UpdateDiagnostics(IReadOnlyList<UnresolvedMappingGroupDiagnostic> diagnostics)
    {
        if (!_diagnosticStore.Update(diagnostics))
        {
            return;
        }

        if (diagnostics.Count == 0)
        {
            LogUnresolvedCleared(_logger);
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            LogUnresolvedGroup(
                _logger,
                diagnostic.GroupIndex,
                diagnostic.Target.DisplayLabel,
                string.Join(", ", diagnostic.MissingCollections.Select(collection => collection.Id.ToString("D"))));
        }
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Warning,
        Message = "Collection Tag Sync mapping group unresolved GroupIndex={GroupIndex} Target={Target} MissingCollectionIds={MissingCollectionIds}")]
    private static partial void LogUnresolvedGroup(
        ILogger logger,
        int groupIndex,
        string target,
        string missingCollectionIds);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync unresolved mapping diagnostics cleared")]
    private static partial void LogUnresolvedCleared(ILogger logger);
}
