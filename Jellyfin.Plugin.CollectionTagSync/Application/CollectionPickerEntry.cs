using System;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Identifies one GUID-backed collection picker choice and its current display name.
/// </summary>
public sealed class CollectionPickerEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionPickerEntry"/> class.
    /// </summary>
    /// <param name="id">The stable Jellyfin collection identity.</param>
    /// <param name="displayName">The current mutable display name.</param>
    public CollectionPickerEntry(Guid id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    /// <summary>Gets the stable Jellyfin collection identity.</summary>
    public Guid Id { get; }

    /// <summary>Gets the current mutable display name.</summary>
    public string DisplayName { get; }
}
