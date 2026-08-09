using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Discovers current direct Movie and Series tag spellings for administrator pickers.
/// </summary>
public interface ITagCatalog
{
    /// <summary>Gets normalized, case-equivalent-deduplicated picker entries.</summary>
    /// <returns>The current direct tag spellings.</returns>
    IReadOnlyList<string> GetPickerEntries();
}
