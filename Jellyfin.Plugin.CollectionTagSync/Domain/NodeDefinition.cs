namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes a node before configuration validation.
/// </summary>
public abstract record NodeDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodeDefinition"/> class.
    /// </summary>
    private protected NodeDefinition()
    {
    }
}
