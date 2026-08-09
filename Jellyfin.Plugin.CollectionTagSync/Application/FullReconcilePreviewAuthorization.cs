using System;
using Jellyfin.Plugin.CollectionTagSync.Configuration;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Returns persisted non-executable preview diagnostics with one short-lived authorization.
/// </summary>
public sealed class FullReconcilePreviewAuthorization
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcilePreviewAuthorization"/> class.
    /// </summary>
    /// <param name="preview">The persisted non-executable diagnostics.</param>
    /// <param name="authorization">The opaque single-use authorization.</param>
    /// <param name="expiresAtUtc">The authorization expiry.</param>
    internal FullReconcilePreviewAuthorization(
        PausedFullReconcileConfiguration preview,
        string authorization,
        DateTimeOffset expiresAtUtc)
    {
        Preview = preview;
        Authorization = authorization;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the persisted non-executable diagnostics.</summary>
    public PausedFullReconcileConfiguration Preview { get; }

    /// <summary>Gets the opaque single-use authorization.</summary>
    public string Authorization { get; }

    /// <summary>Gets the authorization expiry.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}
