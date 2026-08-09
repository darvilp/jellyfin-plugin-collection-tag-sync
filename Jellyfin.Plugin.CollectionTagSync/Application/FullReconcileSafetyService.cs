using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Owns destructive Full Reconcile evaluation and persisted non-executable diagnostics.
/// </summary>
public sealed class FullReconcileSafetyService
{
    private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(10);
    private readonly object _sync = new();
    private readonly Dictionary<string, AuthorizationEntry> _authorizations =
        new(StringComparer.Ordinal);

    private readonly IPluginConfigurationPersistence _persistence;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileSafetyService"/> class.
    /// </summary>
    /// <param name="persistence">The active plugin configuration persistence boundary.</param>
    /// <param name="timeProvider">The authorization and diagnostic clock.</param>
    public FullReconcileSafetyService(
        IPluginConfigurationPersistence persistence,
        TimeProvider timeProvider)
    {
        _persistence = persistence;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates one administrator-bound authorization for persisted paused-plan diagnostics.
    /// </summary>
    /// <param name="runId">The paused run identity.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <returns>The diagnostics and opaque authorization, or <see langword="null"/> when unavailable.</returns>
    public FullReconcilePreviewAuthorization? CreatePreviewAuthorization(
        Guid runId,
        Guid administratorId)
    {
        if (administratorId == Guid.Empty)
        {
            return null;
        }

        lock (_sync)
        {
            var current = _persistence.Current;
            var paused = current.PausedFullReconcile;
            if (paused is null
                || paused.RunId != runId
                || paused.ConfigurationRevision != current.Revision)
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow();
            var staleAuthorizations = _authorizations
                .Where(pair => pair.Value.ExpiresAtUtc <= now
                    || (pair.Value.Confirmation.PausedRunId == runId
                        && pair.Value.AdministratorId == administratorId))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var staleAuthorization in staleAuthorizations)
            {
                _authorizations.Remove(staleAuthorization);
            }

            var expiresAtUtc = now.Add(AuthorizationLifetime);
            var authorization = Guid.NewGuid().ToString("N");
            _authorizations.Add(authorization, new AuthorizationEntry(
                administratorId,
                expiresAtUtc,
                new FullReconcileConfirmation(
                    runId,
                    paused.ConfigurationRevision,
                    PausedFullReconcileConfigurationMapper.ToRemovals(paused))));
            var preview = PausedFullReconcileConfigurationMapper.Clone(paused)!;
            return new FullReconcilePreviewAuthorization(preview, authorization, expiresAtUtc);
        }
    }

    /// <summary>
    /// Evaluates a freshly calculated bulk plan against limits or consumed confirmation.
    /// </summary>
    /// <param name="runId">The opaque fresh-run identity.</param>
    /// <param name="reasons">The coalesced run reasons.</param>
    /// <param name="totalItemCount">The total eligible-item count.</param>
    /// <param name="plans">Every successfully calculated item plan.</param>
    /// <param name="confirmation">The consumed confirmation, when this is a confirmation run.</param>
    /// <returns>Whether the fresh plan may proceed or requires a new preview.</returns>
    internal FullReconcileSafetyDecision Evaluate(
        Guid runId,
        IEnumerable<FullReconcileRequestReason> reasons,
        int totalItemCount,
        IEnumerable<ReconciliationPlan> plans,
        FullReconcileConfirmation? confirmation)
    {
        lock (_sync)
        {
            var current = _persistence.Current;
            var planArray = plans.ToArray();
            var evaluation = DestructiveCircuitBreaker.Evaluate(
                planArray,
                DestructiveCircuitBreakerConfiguration.CreateOptions(current));
            if (confirmation is not null)
            {
                var removalSetMatches = confirmation.ConfigurationRevision == current.Revision
                    && evaluation.Removals.ToHashSet().SetEquals(confirmation.ExpectedRemovals);
                if (removalSetMatches)
                {
                    ClearPaused(current);
                    return FullReconcileSafetyDecision.Proceed;
                }

                PersistPaused(current, runId, reasons, totalItemCount, planArray, evaluation);
                return FullReconcileSafetyDecision.Paused;
            }

            if (evaluation.ShouldPause)
            {
                PersistPaused(current, runId, reasons, totalItemCount, planArray, evaluation);
                return FullReconcileSafetyDecision.Paused;
            }

            ClearPaused(current);
            return FullReconcileSafetyDecision.Proceed;
        }
    }

    /// <summary>
    /// Consumes one valid in-process authorization.
    /// </summary>
    /// <param name="pausedRunId">The paused run identity.</param>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque authorization.</param>
    /// <returns>The in-memory confirmation, or <see langword="null"/> when invalid.</returns>
    internal FullReconcileConfirmation? ConsumeAuthorization(
        Guid pausedRunId,
        Guid administratorId,
        string authorization)
    {
        if (administratorId == Guid.Empty || string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        lock (_sync)
        {
            if (!_authorizations.TryGetValue(authorization, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAtUtc <= _timeProvider.GetUtcNow())
            {
                _authorizations.Remove(authorization);
                return null;
            }

            if (entry.Confirmation.PausedRunId != pausedRunId
                || entry.AdministratorId != administratorId)
            {
                return null;
            }

            _authorizations.Remove(authorization);
            if (_persistence.Current.Revision != entry.Confirmation.ConfigurationRevision)
            {
                return null;
            }

            return entry.Confirmation;
        }
    }

    private void PersistPaused(
        PluginConfiguration current,
        Guid runId,
        IEnumerable<FullReconcileRequestReason> reasons,
        int totalItemCount,
        IEnumerable<ReconciliationPlan> plans,
        DestructiveCircuitBreakerResult evaluation)
    {
        _authorizations.Clear();
        var persisted = PluginConfigurationCloner.Clone(current);
        persisted.PausedFullReconcile = PausedFullReconcileConfigurationMapper.Create(
            runId,
            current.Revision,
            _timeProvider.GetUtcNow().UtcDateTime,
            reasons,
            totalItemCount,
            plans,
            evaluation);
        _persistence.Save(persisted);
    }

    private void ClearPaused(PluginConfiguration current)
    {
        _authorizations.Clear();
        if (current.PausedFullReconcile is null)
        {
            return;
        }

        var persisted = PluginConfigurationCloner.Clone(current);
        persisted.PausedFullReconcile = null;
        _persistence.Save(persisted);
    }

    private sealed class AuthorizationEntry
    {
        public AuthorizationEntry(
            Guid administratorId,
            DateTimeOffset expiresAtUtc,
            FullReconcileConfirmation confirmation)
        {
            AdministratorId = administratorId;
            ExpiresAtUtc = expiresAtUtc;
            Confirmation = confirmation;
        }

        public Guid AdministratorId { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public FullReconcileConfirmation Confirmation { get; }
    }
}
