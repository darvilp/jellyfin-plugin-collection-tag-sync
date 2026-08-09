namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes one candidate configuration validation failure.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The administrator-facing diagnostic message.</param>
/// <param name="GroupIndex">The zero-based candidate group index.</param>
/// <param name="SourceIndex">The zero-based source index, or <see langword="null"/> for a group-level or target diagnostic.</param>
public sealed record MappingValidationError(
    MappingValidationErrorCode Code,
    string Message,
    int GroupIndex,
    int? SourceIndex);
