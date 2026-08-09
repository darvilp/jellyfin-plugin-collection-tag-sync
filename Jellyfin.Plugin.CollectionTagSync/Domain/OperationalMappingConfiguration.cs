using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Contains a fail-closed operational graph and its unresolved-group diagnostics.
/// </summary>
public sealed class OperationalMappingConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationalMappingConfiguration"/> class.
    /// </summary>
    /// <param name="configuration">The derived operational configuration.</param>
    /// <param name="unresolvedGroups">The enabled groups skipped at runtime.</param>
    internal OperationalMappingConfiguration(
        MappingConfiguration configuration,
        IEnumerable<UnresolvedMappingGroupDiagnostic> unresolvedGroups)
    {
        Configuration = configuration;
        UnresolvedGroups = Array.AsReadOnly([.. unresolvedGroups]);
    }

    /// <summary>
    /// Gets the derived mapping configuration used for planning.
    /// </summary>
    public MappingConfiguration Configuration { get; }

    /// <summary>
    /// Gets unresolved enabled groups.
    /// </summary>
    public IReadOnlyList<UnresolvedMappingGroupDiagnostic> UnresolvedGroups { get; }
}
