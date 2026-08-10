using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Validates, previews, safely persists, and enqueues complete continuous configuration candidates.
/// </summary>
public sealed class ConfigurationActivationService : IDisposable
{
    private readonly SemaphoreSlim _activationLock = new(1, 1);
    private readonly IPluginConfigurationPersistence _persistence;
    private readonly IConfigurationCatalog _catalog;
    private readonly IItemStateReader _stateReader;
    private readonly IItemTitleProvider _itemTitleProvider;
    private readonly ConfigurationReconciliationDispatcher _reconciliationDispatcher;
    private readonly BackgroundReconciliationStatusStore _statusStore;
    private readonly ReconciliationExecutionGate _executionGate;
    private readonly ConfigurationPreviewAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActivationService"/> class.
    /// </summary>
    /// <param name="persistence">The Jellyfin plugin configuration persistence boundary.</param>
    /// <param name="catalog">The eligible item and collection catalog.</param>
    /// <param name="stateReader">The direct item-state reader.</param>
    /// <param name="itemTitleProvider">The current Jellyfin item-title boundary.</param>
    /// <param name="reconciliationDispatcher">The background reconciliation dispatcher.</param>
    /// <param name="statusStore">The background status store.</param>
    /// <param name="executionGate">The shared mutation serialization boundary.</param>
    /// <param name="timeProvider">The preview authorization clock.</param>
    public ConfigurationActivationService(
        IPluginConfigurationPersistence persistence,
        IConfigurationCatalog catalog,
        IItemStateReader stateReader,
        IItemTitleProvider itemTitleProvider,
        ConfigurationReconciliationDispatcher reconciliationDispatcher,
        BackgroundReconciliationStatusStore statusStore,
        ReconciliationExecutionGate executionGate,
        TimeProvider timeProvider)
    {
        _persistence = persistence;
        _catalog = catalog;
        _stateReader = stateReader;
        _itemTitleProvider = itemTitleProvider;
        _reconciliationDispatcher = reconciliationDispatcher;
        _statusStore = statusStore;
        _executionGate = executionGate;
        _authorizationService = new ConfigurationPreviewAuthorizationService(timeProvider);
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
            var validation = ValidateCandidate(current, candidate);
            if (validation.Configuration is null)
            {
                return Invalid(current.Revision, validation.Errors);
            }

            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var plan = await CalculatePlanAsync(
                    validation.Configuration,
                    validation.CollectionReferences,
                    cancellationToken).ConfigureAwait(false);
                if (DestructiveRemovalSet.FromPlans(plan.Plans).Count > 0)
                {
                    var pausedId = _statusStore.CreatePaused(current.Revision, plan.ItemIds.Count);
                    return new ConfigurationActivationResult(
                        ConfigurationActivationOutcome.RequiresPreview,
                        current.Revision,
                        pausedId,
                        []);
                }

                return AcceptCandidate(
                    current,
                    candidate,
                    validation.Configuration,
                    validation.DisableIsAcknowledged,
                    plan.ItemIds,
                    plan.Plans);
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

    /// <summary>
    /// Calculates a complete item-level preview without replacing active configuration.
    /// </summary>
    /// <param name="candidate">The complete candidate configuration.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative preview result.</returns>
    public async Task<ConfigurationPreviewResult> PreviewAsync(
        PluginConfiguration candidate,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (administratorId == Guid.Empty)
        {
            throw new ArgumentException("An administrator identity is required.", nameof(administratorId));
        }

        await _activationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _persistence.Current;
            var validation = ValidateCandidate(current, candidate);
            if (validation.Configuration is null)
            {
                return new ConfigurationPreviewResult(
                    ConfigurationPreviewOutcome.Invalid,
                    current.Revision,
                    null,
                    validation.Errors);
            }

            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var plan = await CalculatePlanAsync(
                    validation.Configuration,
                    validation.CollectionReferences,
                    cancellationToken).ConfigureAwait(false);
                var preview = ConfigurationPlanPreviewMapper.Create(
                    current.Revision,
                    plan.ItemIds.Count,
                    plan.Plans,
                    _itemTitleProvider);
                var authorization = _authorizationService.Issue(
                    preview,
                    administratorId,
                    ConfigurationCandidateFingerprint.Create(candidate, validation.Configuration),
                    current.Revision,
                    DestructiveRemovalSet.FromPlans(plan.Plans));
                return new ConfigurationPreviewResult(
                    ConfigurationPreviewOutcome.Ready,
                    current.Revision,
                    authorization,
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

    /// <summary>
    /// Recomputes and conditionally activates one previously previewed candidate.
    /// </summary>
    /// <param name="candidate">The complete candidate configuration.</param>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque preview authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative activation result.</returns>
    public async Task<ConfigurationActivationResult> ConfirmAsync(
        PluginConfiguration candidate,
        Guid administratorId,
        string authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (administratorId == Guid.Empty || string.IsNullOrWhiteSpace(authorization))
        {
            return new ConfigurationActivationResult(
                ConfigurationActivationOutcome.InvalidAuthorization,
                _persistence.Current.Revision,
                null,
                []);
        }

        await _activationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _persistence.Current;
            var validation = ValidateCandidate(current, candidate);
            if (validation.Configuration is null)
            {
                return Invalid(current.Revision, validation.Errors);
            }

            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var plan = await CalculatePlanAsync(
                    validation.Configuration,
                    validation.CollectionReferences,
                    cancellationToken).ConfigureAwait(false);
                var confirmation = _authorizationService.Consume(administratorId, authorization);
                if (confirmation is null)
                {
                    return new ConfigurationActivationResult(
                        ConfigurationActivationOutcome.InvalidAuthorization,
                        current.Revision,
                        null,
                        []);
                }

                var candidateMatches = string.Equals(
                    confirmation.CandidateFingerprint,
                    ConfigurationCandidateFingerprint.Create(candidate, validation.Configuration),
                    StringComparison.Ordinal);
                var removalsMatch = DestructiveRemovalSet
                    .FromPlans(plan.Plans)
                    .ToHashSet()
                    .SetEquals(confirmation.ExpectedRemovals);
                if (confirmation.ActiveRevision != current.Revision
                    || !candidateMatches
                    || !removalsMatch)
                {
                    return new ConfigurationActivationResult(
                        ConfigurationActivationOutcome.RequiresPreview,
                        current.Revision,
                        null,
                        []);
                }

                return AcceptCandidate(
                    current,
                    candidate,
                    validation.Configuration,
                    validation.DisableIsAcknowledged,
                    plan.ItemIds,
                    plan.Plans);
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

    private CandidateValidation ValidateCandidate(
        PluginConfiguration current,
        PluginConfiguration candidate)
    {
        if (!StartupReconcileOptions.IsValidDelay(candidate.StartupReconcileDelayMinutes))
        {
            return CandidateValidation.Invalid(new ConfigurationActivationError(
                ConfigurationActivationErrorCode.InvalidCandidate,
                $"Startup Full Reconcile delay must be between 0 and {StartupReconcileOptions.MaximumDelayMinutes} minutes."));
        }

        if (!DestructiveCircuitBreakerConfiguration.HasValidLimits(candidate))
        {
            return CandidateValidation.Invalid(new ConfigurationActivationError(
                ConfigurationActivationErrorCode.InvalidCandidate,
                $"Destructive circuit-breaker limits require a nonnegative item limit, a percentage from 0 through 100, and a population floor of at least {DestructiveCircuitBreakerOptions.DefaultMinimumAssignmentPopulation}."));
        }

        var currentDisableIsAcknowledged = !current.DestructiveCircuitBreakerEnabled
            && current.DestructiveCircuitBreakerDisableAcknowledged;
        if (!candidate.DestructiveCircuitBreakerEnabled
            && !candidate.DestructiveCircuitBreakerDisableAcknowledged
            && !currentDisableIsAcknowledged)
        {
            return CandidateValidation.Invalid(new ConfigurationActivationError(
                ConfigurationActivationErrorCode.InvalidCandidate,
                "Disabling the destructive circuit breaker requires explicit warning acknowledgment."));
        }

        var validation = PluginConfigurationMapper.ToDomain(candidate);
        if (validation.Configuration is null)
        {
            return CandidateValidation.Invalid(validation.Errors.Select(error =>
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
            return CandidateValidation.Invalid(newlyMissing.Select(id =>
                new ConfigurationActivationError(
                    ConfigurationActivationErrorCode.MissingCollection,
                    $"Collection {id:D} does not exist and cannot be selected.")));
        }

        var disableIsAcknowledged = !candidate.DestructiveCircuitBreakerEnabled
            && (candidate.DestructiveCircuitBreakerDisableAcknowledged
                || currentDisableIsAcknowledged);
        return CandidateValidation.Valid(
            validation.Configuration,
            candidateReferences,
            disableIsAcknowledged);
    }

    private async Task<CandidatePlan> CalculatePlanAsync(
        MappingConfiguration configuration,
        IEnumerable<CollectionReference> candidateReferences,
        CancellationToken cancellationToken)
    {
        var availableCollectionIds = candidateReferences
            .Select(reference => reference.CollectionId)
            .Distinct()
            .Where(_catalog.CollectionExists)
            .ToArray();
        var operational = OperationalMappingResolver.Resolve(
            configuration,
            availableCollectionIds).Configuration;
        var itemIds = _catalog.GetEligibleItemIds().Distinct().ToArray();
        var plans = new List<ReconciliationPlan>();
        foreach (var itemId in itemIds)
        {
            var state = await _stateReader
                .ReadAsync(itemId, operational, cancellationToken)
                .ConfigureAwait(false);
            if (state is not null)
            {
                plans.Add(ReconciliationPlanner.Plan(operational, state));
            }
        }

        return new CandidatePlan(itemIds, plans);
    }

    private ConfigurationActivationResult AcceptCandidate(
        PluginConfiguration current,
        PluginConfiguration candidate,
        MappingConfiguration configuration,
        bool disableIsAcknowledged,
        IReadOnlyList<Guid> itemIds,
        IReadOnlyList<ReconciliationPlan> plans)
    {
        var nextRevision = checked(current.Revision + 1);
        var accepted = CloneWithRevision(current, candidate, nextRevision, disableIsAcknowledged);
        _persistence.Save(accepted);
        var reconciliationId = _reconciliationDispatcher.Enqueue(
            nextRevision,
            itemIds,
            configuration,
            plans);
        return new ConfigurationActivationResult(
            ConfigurationActivationOutcome.Accepted,
            nextRevision,
            reconciliationId,
            []);
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
        PluginConfiguration current,
        PluginConfiguration candidate,
        long revision,
        bool disableIsAcknowledged)
    {
        var accepted = PluginConfigurationCloner.Clone(candidate);
        accepted.RunOnceGroups = PluginConfigurationCloner.Clone(current).RunOnceGroups;
        accepted.Revision = revision;
        accepted.DestructiveCircuitBreakerDisableAcknowledged = disableIsAcknowledged;
        accepted.PausedFullReconcile = null;
        return accepted;
    }

    private sealed record CollectionReference(Node GroupTarget, Guid CollectionId, bool IsTarget);

    private sealed record CandidatePlan(
        IReadOnlyList<Guid> ItemIds,
        IReadOnlyList<ReconciliationPlan> Plans);

    private sealed class CandidateValidation
    {
        private CandidateValidation(
            MappingConfiguration? configuration,
            IEnumerable<CollectionReference> collectionReferences,
            bool disableIsAcknowledged,
            IEnumerable<ConfigurationActivationError> errors)
        {
            Configuration = configuration;
            CollectionReferences = [.. collectionReferences];
            DisableIsAcknowledged = disableIsAcknowledged;
            Errors = [.. errors];
        }

        public MappingConfiguration? Configuration { get; }

        public IReadOnlyList<CollectionReference> CollectionReferences { get; }

        public bool DisableIsAcknowledged { get; }

        public IReadOnlyList<ConfigurationActivationError> Errors { get; }

        public static CandidateValidation Invalid(ConfigurationActivationError error)
        {
            return Invalid([error]);
        }

        public static CandidateValidation Invalid(IEnumerable<ConfigurationActivationError> errors)
        {
            return new CandidateValidation(null, [], false, errors);
        }

        public static CandidateValidation Valid(
            MappingConfiguration configuration,
            IEnumerable<CollectionReference> collectionReferences,
            bool disableIsAcknowledged)
        {
            return new CandidateValidation(
                configuration,
                collectionReferences,
                disableIsAcknowledged,
                []);
        }
    }
}
