using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Resolves current Jellyfin item titles for administrator-facing diagnostics.
/// </summary>
public interface IItemTitleProvider
{
    /// <summary>
    /// Gets the current item title, or an empty string when the item is unavailable.
    /// </summary>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <returns>The current display title.</returns>
    string GetTitle(Guid itemId);
}
