namespace Jellyfin.Plugin.CollectionTagSync.Configuration;

/// <summary>
/// Stores one removal-bearing mapping group's paused-plan diagnostics.
/// </summary>
public sealed class PausedFullReconcileGroupConfiguration
{
    /// <summary>Gets or sets the normalized target identity.</summary>
    public MappingNodeConfiguration Target { get; set; } = new();

    /// <summary>Gets or sets the current direct target-assignment population.</summary>
    public int CurrentAssignmentCount { get; set; }

    /// <summary>Gets or sets the planned removal count.</summary>
    public int RemovalCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the percentage limit was exceeded.</summary>
    public bool ExceedsPercentageLimit { get; set; }
}
