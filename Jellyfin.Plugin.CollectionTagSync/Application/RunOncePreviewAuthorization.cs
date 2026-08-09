using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Returns one complete run-once plan with a short-lived confirmation authorization.
/// </summary>
public sealed class RunOncePreviewAuthorization
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOncePreviewAuthorization"/> class.
    /// </summary>
    /// <param name="preview">The non-executable run-once plan.</param>
    /// <param name="excludableItemIds">Items with a direct operation-target change.</param>
    /// <param name="authorization">The opaque single-use authorization.</param>
    /// <param name="expiresAtUtc">The authorization expiry.</param>
    internal RunOncePreviewAuthorization(
        ConfigurationPlanPreview preview,
        IEnumerable<Guid> excludableItemIds,
        string authorization,
        DateTimeOffset expiresAtUtc)
    {
        Preview = preview;
        ExcludableItemIds = [.. excludableItemIds];
        Authorization = authorization;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the complete non-executable run-once plan.</summary>
    public ConfigurationPlanPreview Preview { get; }

    /// <summary>Gets items whose direct operation-target change may be excluded.</summary>
    public IReadOnlyList<Guid> ExcludableItemIds { get; }

    /// <summary>Gets the opaque single-use authorization.</summary>
    public string Authorization { get; }

    /// <summary>Gets the authorization expiry.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}
