using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes a configured collection node before validation.
/// </summary>
/// <param name="Id">The Jellyfin collection identifier.</param>
/// <param name="DisplayName">The current display name, if known.</param>
public sealed record CollectionNodeDefinition(Guid Id, string? DisplayName = null) : NodeDefinition;
