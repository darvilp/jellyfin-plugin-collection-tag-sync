using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Validates, safely persists, and enqueues complete continuous configuration candidates.
/// </summary>
public sealed class ConfigurationActivationService : IDisposable
{
    private readonly SemaphoreSlim _activationLock = new(1, 1);
    private readonly IPluginConfigurationPersistence _persistence;
    private readonly IConfigurationCatalog _catalog;
    private readonly IItemStateReader _stateReader;
    private readonly ConfigurationReconciliationDispatcher _reconciliationDispatcher;
    private readonly BackgroundReconciliationStatusStore _statusStore;
    private readonly ReconciliationExecutionGate _executionGate;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActivationService"/> class.
    /// </summary>
    /// <param name="persistence">The Jellyfin plugin configuration persistence boundary.</param>
    /// <param name="catalog">The eligible item and collection catalog.</param>
    /// <param name="stateReader">The direct item-state reader.</param>
    /// <param name="reconciliationDispatcher">The background reconciliation dispatcher.</param>
    /// <param name="statusStore">The background status store.</param>
    /// <param name="executionGate">The shared mutation serialization boundary.</param>
    public ConfigurationActivationService(
        IPluginConfigurationPersistence persistence,
        IConfigurationCatalog catalog,
        IItemStateReader stateReader,
        ConfigurationReconciliationDispatcher reconciliationDispatcher,
        BackgroundReconciliationStatusStore statusStore,
        ReconciliationExecutionGate executionGate)
    {
        _persistence = persistence;
        _catalog = catalog;
        _stateReader = stateReader;
        _reconciliationDispatcher = reconciliationDispatcher;
        _statusStore = statusStore;
        _executionGate = executionGate;
    }

    /// <summary>
    /// Validates and activates one complete ordinary configuration candidate.
    /// </summary>
    /// <param name="candidate">The complete candidate configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative activation result.</returns>
    public async Task<ConfigurationActivationResult> ActivateAsync(
        PluginConfiguration candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await _activationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _persistence.Current;
            if (!StartupReconcileOptions.IsValidDelay(candidate.StartupReconcileDelayMinutes))
            {
                return Invalid(
                    current.Revision,
                    [new ConfigurationActivationError(
                        ConfigurationActivationErrorCode.InvalidCandidate,
                        $"Startup Full Reconcile delay must be between 0 and {StartupReconcileOptions.MaximumDelayMinutes} minutes.")]);
            }

            if (!DestructiveCircuitBreakerConfiguration.HasValidLimits(candidate))
            {
                return Invalid(
                    current.Revision,
                    [new ConfigurationActivationError(
                        ConfigurationActivationErrorCode.InvalidCandidate,
                        $"Destructive circuit-breaker limits require a nonnegative item limit, a percentage from 0 through 100, and a population floor of at least {DestructiveCircuitBreakerOptions.DefaultMinimumAssignmentPopulation}.")]);
            }

            var currentDisableIsAcknowledged = !current.DestructiveCircuitBreakerEnabled
                && current.DestructiveCircuitBreakerDisableAcknowledged;
            if (!candidate.DestructiveCircuitBreakerEnabled
                && !candidate.DestructiveCircuitBreakerDisableAcknowledged
                && !currentDisableIsAcknowledged)
            {
                return Invalid(
                    current.Revision,
                    [new ConfigurationActivationError(
                        ConfigurationActivationErrorCode.InvalidCandidate,
                        "Disabling the destructive circuit breaker requires explicit warning acknowledgment.")]);
            }

            var validation = PluginConfigurationMapper.ToDomain(candidate);
            if (validation.Configuration is null)
            {
                return Invalid(current.Revision, validation.Errors.Select(error =>
                    new ConfigurationActivationError(
                        ConfigurationActivationErrorCode.InvalidCandidate,
                        error.Message)));
            }

            var currentValidation = PluginConfigurationMapper.ToDomain(current);
            var currentReferences = currentValidation.Configuration is null
                ? []
                : GetCollectionReferences(currentValidation.Configuration).ToHashSet();
            var candidateReferences = GetCollectionReferences(validation.Configuration).ToArray();
            var newlyMissing = candidateReferences
                .Where(reference =>
                    !currentReferences.Contains(reference)
                    && !_catalog.CollectionExists(reference.CollectionId))
                .Select(reference => reference.CollectionId)
                .Distinct()
                .ToArray();
            if (newlyMissing.Length > 0)
            {
                return Invalid(current.Revision, newlyMissing.Select(id =>
                    new ConfigurationActivationError(
                        ConfigurationActivationErrorCode.MissingCollection,
                        $"Collection {id:D} does not exist and cannot be selected.")));
            }

            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var candidateCollectionIds = candidateReferences
                    .Select(reference => reference.CollectionId)
                    .Distinct()
                    .ToArray();
                var availableCollectionIds = candidateCollectionIds
                    .Where(_catalog.CollectionExists)
                    .ToArray();
                var operational = OperationalMappingResolver.Resolve(
                    validation.Configuration,
                    availableCollectionIds).Configuration;
                var itemIds = _catalog.GetEligibleItemIds().Distinct().ToArray();
                var hasRemoval = false;
                foreach (var itemId in itemIds)
                {
                    var state = await _stateReader
                        .ReadAsync(itemId, operational, cancellationToken)
                        .ConfigureAwait(false);
                    if (state is null)
                    {
                        continue;
                    }

                    var plan = ReconciliationPlanner.Plan(operational, state);
                    hasRemoval |= plan.Mutations.Any(mutation => mutation.Kind is
                        PlannedMutationKind.RemoveTag or PlannedMutationKind.RemoveCollectionMembership);
                }

                if (hasRemoval)
                {
                    var pausedId = _statusStore.CreatePaused(current.Revision, itemIds.Length);
                    return new ConfigurationActivationResult(
                        ConfigurationActivationOutcome.RequiresPreview,
                        current.Revision,
                        pausedId,
                        []);
                }

                var nextRevision = checked(current.Revision + 1);
                var disableIsAcknowledged = !candidate.DestructiveCircuitBreakerEnabled
                    && (candidate.DestructiveCircuitBreakerDisableAcknowledged
                        || currentDisableIsAcknowledged);
                var accepted = CloneWithRevision(candidate, nextRevision, disableIsAcknowledged);
                _persistence.Save(accepted);
                var reconciliationId = _reconciliationDispatcher.Enqueue(
                    nextRevision,
                    itemIds,
                    validation.Configuration);
                return new ConfigurationActivationResult(
                    ConfigurationActivationOutcome.Accepted,
                    nextRevision,
                    reconciliationId,
                    []);
            }
            finally
            {
                _executionGate.Exit();
            }
        }
        finally
        {
            _activationLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activationLock.Dispose();
    }

    private static ConfigurationActivationResult Invalid(
        long activeRevision,
        IEnumerable<ConfigurationActivationError> errors)
    {
        return new ConfigurationActivationResult(
            ConfigurationActivationOutcome.Invalid,
            activeRevision,
            null,
            errors);
    }

    private static IEnumerable<CollectionReference> GetCollectionReferences(
        MappingConfiguration configuration)
    {
        foreach (var group in configuration.Groups)
        {
            if (group.Target is CollectionNode target)
            {
                yield return new CollectionReference(group.Target, target.Id, IsTarget: true);
            }

            foreach (var source in group.Sources.OfType<CollectionNode>())
            {
                yield return new CollectionReference(group.Target, source.Id, IsTarget: false);
            }
        }
    }

    private static PluginConfiguration CloneWithRevision(
        PluginConfiguration candidate,
        long revision,
        bool disableIsAcknowledged)
    {
        var accepted = PluginConfigurationCloner.Clone(candidate);
        accepted.Revision = revision;
        accepted.DestructiveCircuitBreakerDisableAcknowledged = disableIsAcknowledged;
        accepted.PausedFullReconcile = null;
        return accepted;
    }

    private sealed record CollectionReference(Node GroupTarget, Guid CollectionId, bool IsTarget);
}
