using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Issues and consumes restart-scoped run-once preview authorizations.
/// </summary>
internal sealed class RunOncePreviewAuthorizationService
{
    private readonly PreviewAuthorizationStore<RunOncePreviewConfirmation> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunOncePreviewAuthorizationService"/> class.
    /// </summary>
    /// <param name="timeProvider">The authorization clock.</param>
    public RunOncePreviewAuthorizationService(TimeProvider timeProvider)
    {
        _store = new PreviewAuthorizationStore<RunOncePreviewConfirmation>(timeProvider);
    }

    /// <summary>Issues one operation-bound authorization.</summary>
    /// <param name="preview">The complete non-executable plan.</param>
    /// <param name="excludableItemIds">Items with a direct operation-target change.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="operationFingerprint">The canonical operation and exclusion identity.</param>
    /// <param name="activeRevision">The active revision used during planning.</param>
    /// <param name="removals">The exact planned removal tuples.</param>
    /// <returns>The complete preview and opaque authorization.</returns>
    public RunOncePreviewAuthorization Issue(
        ConfigurationPlanPreview preview,
        IEnumerable<Guid> excludableItemIds,
        Guid administratorId,
        string operationFingerprint,
        long activeRevision,
        IEnumerable<DestructiveRemoval> removals)
    {
        var grant = _store.Issue(
            administratorId,
            new RunOncePreviewConfirmation(
                operationFingerprint,
                activeRevision,
                removals));
        return new RunOncePreviewAuthorization(
            preview,
            excludableItemIds,
            grant.Authorization,
            grant.ExpiresAtUtc);
    }

    /// <summary>Consumes one valid administrator-bound authorization.</summary>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque authorization.</param>
    /// <returns>The immutable confirmation payload, or <see langword="null"/>.</returns>
    public RunOncePreviewConfirmation? Consume(Guid administratorId, string authorization)
    {
        return _store.Consume(administratorId, authorization);
    }
}
