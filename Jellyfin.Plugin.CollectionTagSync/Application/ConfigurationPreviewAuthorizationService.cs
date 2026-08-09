using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Issues and consumes candidate-configuration preview authorizations.
/// </summary>
public sealed class ConfigurationPreviewAuthorizationService
{
    private readonly PreviewAuthorizationStore<ConfigurationPreviewConfirmation> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationPreviewAuthorizationService"/> class.
    /// </summary>
    /// <param name="timeProvider">The authorization clock.</param>
    public ConfigurationPreviewAuthorizationService(TimeProvider timeProvider)
    {
        _store = new PreviewAuthorizationStore<ConfigurationPreviewConfirmation>(timeProvider);
    }

    /// <summary>Issues one candidate-bound authorization.</summary>
    /// <param name="preview">The complete non-executable candidate plan.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="candidateFingerprint">The canonical candidate identity.</param>
    /// <param name="activeRevision">The active revision used during planning.</param>
    /// <param name="removals">The exact planned removal tuples.</param>
    /// <returns>The complete preview and opaque authorization.</returns>
    internal ConfigurationPreviewAuthorization Issue(
        ConfigurationPlanPreview preview,
        Guid administratorId,
        string candidateFingerprint,
        long activeRevision,
        IEnumerable<DestructiveRemoval> removals)
    {
        var grant = _store.Issue(
            administratorId,
            new ConfigurationPreviewConfirmation(
                candidateFingerprint,
                activeRevision,
                removals));
        return new ConfigurationPreviewAuthorization(
            preview,
            grant.Authorization,
            grant.ExpiresAtUtc);
    }

    /// <summary>Consumes one valid administrator-bound authorization.</summary>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque authorization.</param>
    /// <returns>The immutable confirmation payload, or <see langword="null"/>.</returns>
    internal ConfigurationPreviewConfirmation? Consume(
        Guid administratorId,
        string authorization)
    {
        return _store.Consume(administratorId, authorization);
    }
}
