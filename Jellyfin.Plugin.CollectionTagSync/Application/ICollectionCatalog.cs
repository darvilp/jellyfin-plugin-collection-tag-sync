using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Lists GUID-backed collection choices and performs independent Jellyfin creation.
/// </summary>
public interface ICollectionCatalog
{
    /// <summary>Gets the current collection picker choices.</summary>
    /// <returns>Every current collection as a distinct GUID-backed entry.</returns>
    IReadOnlyList<CollectionPickerEntry> GetPickerEntries();

    /// <summary>Creates one independent empty Jellyfin collection.</summary>
    /// <param name="displayName">The already validated trimmed display name.</param>
    /// <returns>The Jellyfin-created collection identity and current display name.</returns>
    Task<CollectionPickerEntry> CreateAsync(string displayName);
}
