using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Contains either a validated configuration or explicit diagnostics.
/// </summary>
public sealed class MappingConfigurationValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingConfigurationValidationResult"/> class.
    /// </summary>
    /// <param name="configuration">The validated configuration, if successful.</param>
    /// <param name="errors">The validation diagnostics.</param>
    internal MappingConfigurationValidationResult(
        MappingConfiguration? configuration,
        IEnumerable<MappingValidationError> errors)
    {
        Configuration = configuration;
        Errors = Array.AsReadOnly([.. errors]);
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid => Configuration is not null;

    /// <summary>
    /// Gets the validated configuration, or <see langword="null"/> on failure.
    /// </summary>
    public MappingConfiguration? Configuration { get; }

    /// <summary>
    /// Gets validation diagnostics.
    /// </summary>
    public IReadOnlyList<MappingValidationError> Errors { get; }
}
