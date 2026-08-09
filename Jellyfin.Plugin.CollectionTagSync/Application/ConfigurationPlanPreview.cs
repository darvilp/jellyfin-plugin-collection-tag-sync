using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Contains the complete settled-state plan for one candidate configuration.
/// </summary>
public sealed class ConfigurationPlanPreview
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationPlanPreview"/> class.
    /// </summary>
    /// <param name="activeConfigurationRevision">The active revision used during planning.</param>
    /// <param name="totalItemCount">The eligible item count.</param>
    /// <param name="items">Every successfully planned item.</param>
    internal ConfigurationPlanPreview(
        long activeConfigurationRevision,
        int totalItemCount,
        IEnumerable<ConfigurationItemPlanPreview> items)
    {
        ActiveConfigurationRevision = activeConfigurationRevision;
        TotalItemCount = totalItemCount;
        Items = [.. items];
    }

    /// <summary>Gets the active revision used during planning.</summary>
    public long ActiveConfigurationRevision { get; }

    /// <summary>Gets the eligible item count.</summary>
    public int TotalItemCount { get; }

    /// <summary>Gets every successfully planned item.</summary>
    public IReadOnlyList<ConfigurationItemPlanPreview> Items { get; }
}
