namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Carries the display name for one explicit independent collection creation.
/// </summary>
public sealed class CollectionCreationRequest
{
    /// <summary>Gets or sets the proposed display name.</summary>
    public string Name { get; set; } = string.Empty;
}
