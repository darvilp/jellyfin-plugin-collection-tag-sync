using System;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Selects one persisted run-once group and carries execution-only exclusions.
/// </summary>
public sealed class SavedRunOnceOperationRequest
{
    /// <summary>Gets or sets the selected persisted group identity.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets items that retain their observed direct target state for this execution.</summary>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Jellyfin API requests require simple settable serializer DTOs.")]
    public Guid[] ExcludedItemIds { get; set; } = [];
}
