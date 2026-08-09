namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes a configured tag node before validation.
/// </summary>
/// <param name="Value">The administrator-configured tag spelling.</param>
public sealed record TagNodeDefinition(string? Value) : NodeDefinition;
