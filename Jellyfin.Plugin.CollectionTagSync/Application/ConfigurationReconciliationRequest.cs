using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries one accepted configuration reconciliation to the background worker.
/// </summary>
internal sealed class ConfigurationReconciliationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationReconciliationRequest"/> class.
    /// </summary>
    /// <param name="id">The opaque request identity.</param>
    /// <param name="revision">The accepted configuration revision.</param>
    /// <param name="itemIds">The eligible item identities.</param>
    /// <param name="configuration">The immutable accepted configuration.</param>
    public ConfigurationReconciliationRequest(
        Guid id,
        long revision,
        IEnumerable<Guid> itemIds,
        MappingConfiguration configuration)
    {
        Id = id;
        Revision = revision;
        ItemIds = Array.AsReadOnly([.. itemIds]);
        Configuration = configuration;
    }

    /// <summary>
    /// Gets the opaque request identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the accepted configuration revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the eligible item identities.
    /// </summary>
    public IReadOnlyList<Guid> ItemIds { get; }

    /// <summary>
    /// Gets the immutable accepted configuration for this revision.
    /// </summary>
    public MappingConfiguration Configuration { get; }
}
