using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Describes one candidate configuration validation failure.
/// </summary>
public sealed class MappingValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingValidationError"/> class.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The administrator-facing diagnostic message.</param>
    /// <param name="groupIndex">The zero-based candidate group index.</param>
    /// <param name="sourceIndex">The zero-based source index, or <see langword="null"/> for a group-level or target diagnostic.</param>
    /// <param name="cyclePath">The complete cycle path, when applicable.</param>
    internal MappingValidationError(
        MappingValidationErrorCode code,
        string message,
        int groupIndex,
        int? sourceIndex,
        IEnumerable<Node>? cyclePath = null)
    {
        Code = code;
        Message = message;
        GroupIndex = groupIndex;
        SourceIndex = sourceIndex;
        CyclePath = Array.AsReadOnly(cyclePath is null ? Array.Empty<Node>() : [.. cyclePath]);
    }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    public MappingValidationErrorCode Code { get; }

    /// <summary>
    /// Gets the administrator-facing diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the zero-based candidate group index.
    /// </summary>
    public int GroupIndex { get; }

    /// <summary>
    /// Gets the zero-based source index, or <see langword="null"/> for a group-level or target diagnostic.
    /// </summary>
    public int? SourceIndex { get; }

    /// <summary>
    /// Gets the complete cycle path, or an empty list for non-cycle diagnostics.
    /// </summary>
    public IReadOnlyList<Node> CyclePath { get; }
}
