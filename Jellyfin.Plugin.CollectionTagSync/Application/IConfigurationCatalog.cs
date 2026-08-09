using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides the Jellyfin identities needed to validate and reconcile configuration.
/// </summary>
public interface IConfigurationCatalog
{
    /// <summary>
    /// Gets every direct Movie and Series identity eligible for V1 reconciliation.
    /// </summary>
    /// <returns>The eligible item identities.</returns>
    IReadOnlyList<Guid> GetEligibleItemIds();

    /// <summary>
    /// Determines whether a collection GUID currently resolves to a Jellyfin collection.
    /// </summary>
    /// <param name="collectionId">The collection identity.</param>
    /// <returns><see langword="true"/> when the collection resolves.</returns>
    bool CollectionExists(Guid collectionId);
}
