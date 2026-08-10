using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Validates, previews, authorizes, and queues ephemeral mapping-shaped operations.
/// </summary>
public sealed class RunOnceService : IDisposable
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly IPluginConfigurationPersistence _persistence;
    private readonly IConfigurationCatalog _catalog;
    private readonly IItemStateReader _stateReader;
    private readonly IItemTitleProvider _itemTitleProvider;
    private readonly ConfigurationReconciliationDispatcher _dispatcher;
    private readonly ReconciliationExecutionGate _executionGate;
    private readonly RunOncePreviewAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceService"/> class.
    /// </summary>
    /// <param name="persistence">The active plugin configuration boundary.</param>
    /// <param name="catalog">The eligible item and collection catalog.</param>
    /// <param name="stateReader">The direct item-state reader.</param>
    /// <param name="itemTitleProvider">The current Jellyfin item-title boundary.</param>
    /// <param name="dispatcher">The exact-plan background dispatcher.</param>
    /// <param name="executionGate">The shared mutation serialization boundary.</param>
    /// <param name="timeProvider">The preview authorization clock.</param>
    public RunOnceService(
        IPluginConfigurationPersistence persistence,
        IConfigurationCatalog catalog,
        IItemStateReader stateReader,
        IItemTitleProvider itemTitleProvider,
        ConfigurationReconciliationDispatcher dispatcher,
        ReconciliationExecutionGate executionGate,
        TimeProvider timeProvider)
    {
        _persistence = persistence;
        _catalog = catalog;
        _stateReader = stateReader;
        _itemTitleProvider = itemTitleProvider;
        _dispatcher = dispatcher;
        _executionGate = executionGate;
        _authorizationService = new RunOncePreviewAuthorizationService(timeProvider);
    }

    /// <summary>
    /// Gets independent snapshots of all persisted reusable run-once groups.
    /// </summary>
    /// <returns>The persisted groups in administrator-defined order.</returns>
    public IReadOnlyList<RunOnceGroupConfiguration> GetGroups()
    {
        return PluginConfigurationCloner.Clone(_persistence.Current).RunOnceGroups;
    }

    /// <summary>
    /// Validates and persists one new or existing reusable run-once group.
    /// </summary>
    /// <param name="candidate">The complete group candidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-normalized persisted group or validation failures.</returns>
    public async Task<RunOnceGroupSaveResult> SaveGroupAsync(
        RunOnceGroupConfiguration candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = _persistence.Current;
                var existingGroups = current.RunOnceGroups ?? [];
                var existingIndex = candidate.Id == Guid.Empty
                    ? -1
                    : Array.FindIndex(existingGroups, group => group.Id == candidate.Id);
                if (candidate.Id != Guid.Empty && existingIndex < 0)
                {
                    return new RunOnceGroupSaveResult(
                        RunOnceGroupSaveOutcome.Invalid,
                        null,
                        [MissingGroupError(candidate.Id)]);
                }

                var request = CreateOperationRequest(candidate, []);
                var validation = Validate(current, request);
                if (validation.Operation is null)
                {
                    return new RunOnceGroupSaveResult(
                        RunOnceGroupSaveOutcome.Invalid,
                        null,
                        validation.Errors);
                }

                var persistedGroup = PluginConfigurationCloner.CloneRunOnceGroup(candidate);
                persistedGroup.Id = candidate.Id == Guid.Empty ? Guid.NewGuid() : candidate.Id;
                var persisted = PluginConfigurationCloner.Clone(current);
                var groups = (persisted.RunOnceGroups ?? []).ToList();
                if (existingIndex >= 0)
                {
                    groups[existingIndex] = persistedGroup;
                }
                else
                {
                    groups.Add(persistedGroup);
                }

                persisted.RunOnceGroups = [.. groups];
                _persistence.Save(persisted);
                if (existingIndex >= 0)
                {
                    _authorizationService.InvalidateGroup(persistedGroup.Id);
                }

                return new RunOnceGroupSaveResult(
                    RunOnceGroupSaveOutcome.Saved,
                    PluginConfigurationCloner.CloneRunOnceGroup(persistedGroup),
                    []);
            }
            finally
            {
                _executionGate.Exit();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Deletes one persisted reusable run-once group without changing continuous configuration revision.
    /// </summary>
    /// <param name="groupId">The stable group identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a group was removed.</returns>
    public async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (groupId == Guid.Empty)
        {
            return false;
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var persisted = PluginConfigurationCloner.Clone(_persistence.Current);
                var groups = (persisted.RunOnceGroups ?? []).Where(group => group.Id != groupId).ToArray();
                if (groups.Length == (persisted.RunOnceGroups ?? []).Length)
                {
                    return false;
                }

                persisted.RunOnceGroups = groups;
                _persistence.Save(persisted);
                _authorizationService.InvalidateGroup(groupId);
                return true;
            }
            finally
            {
                _executionGate.Exit();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Calculates a complete preview for exactly one persisted run-once group.
    /// </summary>
    /// <param name="request">The selected group and ephemeral exclusions.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative preview result.</returns>
    public Task<RunOncePreviewResult> PreviewSavedAsync(
        SavedRunOnceOperationRequest request,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PreviewCoreAsync(null, request, administratorId, cancellationToken);
    }

    /// <summary>
    /// Recomputes and conditionally queues exactly one persisted run-once group.
    /// </summary>
    /// <param name="request">The selected group and exact ephemeral exclusions.</param>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque preview authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative confirmation result.</returns>
    public Task<RunOnceExecutionResult> ConfirmSavedAsync(
        SavedRunOnceOperationRequest request,
        Guid administratorId,
        string authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ConfirmCoreAsync(null, request, administratorId, authorization, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _operationLock.Dispose();
    }

    /// <summary>
    /// Calculates a transient run-once preview for focused application tests.
    /// </summary>
    /// <param name="request">The operation and ephemeral exclusion set.</param>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative preview result.</returns>
    internal Task<RunOncePreviewResult> PreviewAsync(
        RunOnceOperationRequest request,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PreviewCoreAsync(request, null, administratorId, cancellationToken);
    }

    /// <summary>
    /// Confirms a transient run-once request for focused application tests.
    /// </summary>
    /// <param name="request">The exact operation and exclusion set.</param>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque preview authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server-authoritative confirmation result.</returns>
    internal Task<RunOnceExecutionResult> ConfirmAsync(
        RunOnceOperationRequest request,
        Guid administratorId,
        string authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ConfirmCoreAsync(request, null, administratorId, authorization, cancellationToken);
    }

    private async Task<RunOncePreviewResult> PreviewCoreAsync(
        RunOnceOperationRequest? request,
        SavedRunOnceOperationRequest? savedRequest,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        if (administratorId == Guid.Empty)
        {
            throw new ArgumentException("An administrator identity is required.", nameof(administratorId));
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = _persistence.Current;
                var resolvedRequest = ResolveRequest(current, request, savedRequest, out var groupId);
                if (resolvedRequest is null)
                {
                    return InvalidPreview(
                        current.Revision,
                        [MissingGroupError(savedRequest?.GroupId ?? Guid.Empty)]);
                }

                var validation = Validate(current, resolvedRequest);
                if (validation.Operation is null)
                {
                    return InvalidPreview(current.Revision, validation.Errors);
                }

                var candidatePlan = await CalculatePlanAsync(
                    validation,
                    cancellationToken).ConfigureAwait(false);
                if (candidatePlan.Errors.Count > 0)
                {
                    return InvalidPreview(current.Revision, candidatePlan.Errors);
                }

                var preview = ConfigurationPlanPreviewMapper.Create(
                    current.Revision,
                    candidatePlan.TotalItemCount,
                    candidatePlan.Plans,
                    _itemTitleProvider);
                var authorization = _authorizationService.Issue(
                    preview,
                    candidatePlan.ExcludableItemIds,
                    administratorId,
                    groupId,
                    RunOnceOperationFingerprint.Create(
                        groupId,
                        validation.Operation,
                        validation.ExcludedItemIds),
                    current.Revision,
                    DestructiveRemovalSet.FromPlans(candidatePlan.Plans));
                return new RunOncePreviewResult(
                    RunOncePreviewOutcome.Ready,
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
            _operationLock.Release();
        }
    }

    private async Task<RunOnceExecutionResult> ConfirmCoreAsync(
        RunOnceOperationRequest? request,
        SavedRunOnceOperationRequest? savedRequest,
        Guid administratorId,
        string authorization,
        CancellationToken cancellationToken)
    {
        if (administratorId == Guid.Empty || string.IsNullOrWhiteSpace(authorization))
        {
            return ExecutionResult(
                RunOnceExecutionOutcome.InvalidAuthorization,
                _persistence.Current.Revision);
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _executionGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = _persistence.Current;
                var resolvedRequest = ResolveRequest(current, request, savedRequest, out var groupId);
                if (resolvedRequest is null)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.RequiresPreview,
                        current.Revision);
                }

                var confirmation = _authorizationService.Consume(administratorId, authorization);
                if (confirmation is null)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.InvalidAuthorization,
                        current.Revision);
                }

                if (confirmation.ActiveRevision != current.Revision)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.RequiresPreview,
                        current.Revision);
                }

                var validation = Validate(current, resolvedRequest);
                if (validation.Operation is null)
                {
                    return new RunOnceExecutionResult(
                        RunOnceExecutionOutcome.Invalid,
                        current.Revision,
                        null,
                        validation.Errors);
                }

                var operationMatches = string.Equals(
                    confirmation.OperationFingerprint,
                    RunOnceOperationFingerprint.Create(
                        groupId,
                        validation.Operation,
                        validation.ExcludedItemIds),
                    StringComparison.Ordinal);
                if (!operationMatches)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.RequiresPreview,
                        current.Revision);
                }

                var candidatePlan = await CalculatePlanAsync(
                    validation,
                    cancellationToken).ConfigureAwait(false);
                if (candidatePlan.Errors.Count > 0)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.RequiresPreview,
                        current.Revision);
                }

                var removalsMatch = DestructiveRemovalSet
                    .FromPlans(candidatePlan.Plans)
                    .ToHashSet()
                    .SetEquals(confirmation.ExpectedRemovals);
                if (!removalsMatch)
                {
                    return ExecutionResult(
                        RunOnceExecutionOutcome.RequiresPreview,
                        current.Revision);
                }

                var reconciliationId = _dispatcher.Enqueue(
                    current.Revision,
                    candidatePlan.ItemIds,
                    validation.ActiveOperationalConfiguration,
                    candidatePlan.Plans);
                return new RunOnceExecutionResult(
                    RunOnceExecutionOutcome.Accepted,
                    current.Revision,
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
            _operationLock.Release();
        }
    }

    private static RunOnceOperationRequest? ResolveRequest(
        PluginConfiguration current,
        RunOnceOperationRequest? transientRequest,
        SavedRunOnceOperationRequest? savedRequest,
        out Guid groupId)
    {
        if (transientRequest is not null)
        {
            groupId = Guid.Empty;
            return transientRequest;
        }

        groupId = savedRequest?.GroupId ?? Guid.Empty;
        var selectedGroupId = groupId;
        var group = (current.RunOnceGroups ?? [])
            .SingleOrDefault(candidate => candidate.Id == selectedGroupId);
        return group is null
            ? null
            : CreateOperationRequest(group, savedRequest?.ExcludedItemIds ?? []);
    }

    private static RunOnceOperationRequest CreateOperationRequest(
        RunOnceGroupConfiguration group,
        Guid[] excludedItemIds)
    {
        return new RunOnceOperationRequest
        {
            Target = group.Target,
            Sources = group.Sources ?? [],
            Policy = group.Policy,
            ExcludedItemIds = excludedItemIds,
        };
    }

    private static RunOnceValidationError MissingGroupError(Guid groupId)
    {
        return new RunOnceValidationError(
            RunOnceValidationErrorCode.MissingGroup,
            $"Saved run-once group {groupId:D} does not exist.");
    }

    private OperationValidation Validate(
        PluginConfiguration current,
        RunOnceOperationRequest request)
    {
        var activeValidation = PluginConfigurationMapper.ToDomain(current);
        if (activeValidation.Configuration is null)
        {
            return OperationValidation.Invalid(activeValidation.Errors.Select(error =>
                new RunOnceValidationError(
                    RunOnceValidationErrorCode.InvalidOperation,
                    $"The active configuration is invalid: {error.Message}")));
        }

        var operationValidation = MappingConfiguration.Create(
        [
            new MappingGroupDefinition(
                MappingNodeConfigurationMapper.ToDefinition(request.Target),
                (request.Sources ?? []).Select(MappingNodeConfigurationMapper.ToDefinition),
                request.Policy,
                isEnabled: true),
        ]);
        if (operationValidation.Configuration is null)
        {
            return OperationValidation.Invalid(operationValidation.Errors.Select(error =>
                new RunOnceValidationError(
                    RunOnceValidationErrorCode.InvalidOperation,
                    error.Message)));
        }

        var operationGroup = AssertSingleGroup(operationValidation.Configuration);
        if (activeValidation.Configuration.Groups.Any(group =>
            group.IsEnabled && group.Target.Equals(operationGroup.Target)))
        {
            return OperationValidation.Invalid(new RunOnceValidationError(
                RunOnceValidationErrorCode.TargetConflict,
                "An enabled continuous mapping already manages the requested run-once target."));
        }

        var missingCollections = operationGroup.Sources
            .Append(operationGroup.Target)
            .OfType<CollectionNode>()
            .Select(collection => collection.Id)
            .Distinct()
            .Where(id => !_catalog.CollectionExists(id))
            .ToArray();
        if (missingCollections.Length > 0)
        {
            return OperationValidation.Invalid(missingCollections.Select(id =>
                new RunOnceValidationError(
                    RunOnceValidationErrorCode.MissingCollection,
                    $"Collection {id:D} does not exist and cannot be selected.")));
        }

        var activeCollectionIds = activeValidation.Configuration.Groups
            .SelectMany(group => group.Sources.Append(group.Target))
            .OfType<CollectionNode>()
            .Select(collection => collection.Id)
            .Distinct()
            .Where(_catalog.CollectionExists);
        var activeOperational = OperationalMappingResolver.Resolve(
            activeValidation.Configuration,
            activeCollectionIds).Configuration;
        return OperationValidation.Valid(
            activeOperational,
            operationValidation.Configuration,
            new RunOnceOperation(
                operationGroup.Target,
                operationGroup.Sources,
                operationGroup.Policy),
            (request.ExcludedItemIds ?? []).Distinct());
    }

    private async Task<CandidatePlan> CalculatePlanAsync(
        OperationValidation validation,
        CancellationToken cancellationToken)
    {
        var operation = validation.Operation!;
        var eligibleItemIds = _catalog.GetEligibleItemIds().Distinct().ToArray();
        var states = new List<ObservedItemState>();
        foreach (var itemId in eligibleItemIds)
        {
            var activeState = await _stateReader
                .ReadAsync(
                    itemId,
                    validation.ActiveOperationalConfiguration,
                    cancellationToken)
                .ConfigureAwait(false);
            var operationState = await _stateReader
                .ReadAsync(
                    itemId,
                    validation.OperationConfiguration,
                    cancellationToken)
                .ConfigureAwait(false);
            var combined = Combine(activeState, operationState);
            if (combined is not null)
            {
                states.Add(combined);
            }
        }

        var selection = RunOnceCandidateSelector.SelectPlanDetails(
            validation.ActiveOperationalConfiguration,
            operation,
            states,
            validation.ExcludedItemIds);
        var invalidExclusions = validation.ExcludedItemIds
            .Where(itemId => !selection.DirectTargetChangeItemIds.Contains(itemId))
            .ToArray();
        if (invalidExclusions.Length > 0)
        {
            return CandidatePlan.Invalid(
                eligibleItemIds.Length,
                invalidExclusions.Select(itemId => new RunOnceValidationError(
                    RunOnceValidationErrorCode.InvalidExclusion,
                    $"Item {itemId:D} is not a current eligible direct run-once target change.")));
        }

        return CandidatePlan.Valid(
            eligibleItemIds.Length,
            selection.Plans.Select(plan => plan.ItemId),
            selection.Plans,
            selection.DirectTargetChangeItemIds);
    }

    private static ObservedItemState? Combine(
        ObservedItemState? activeState,
        ObservedItemState? operationState)
    {
        var primary = activeState ?? operationState;
        if (primary is null)
        {
            return null;
        }

        var secondary = ReferenceEquals(primary, activeState) ? operationState : activeState;
        return new ObservedItemState(
            primary.ItemId,
            primary.ItemKind,
            primary.DirectTags
                .Concat(secondary?.DirectTags ?? [])
                .Distinct(StringComparer.Ordinal),
            primary.DirectCollectionIds
                .Concat(secondary?.DirectCollectionIds ?? [])
                .Distinct());
    }

    private static MappingGroup AssertSingleGroup(MappingConfiguration configuration)
    {
        return configuration.Groups.Count == 1
            ? configuration.Groups[0]
            : throw new InvalidOperationException("A run-once operation must contain exactly one mapping group.");
    }

    private static RunOncePreviewResult InvalidPreview(
        long activeRevision,
        IEnumerable<RunOnceValidationError> errors)
    {
        return new RunOncePreviewResult(
            RunOncePreviewOutcome.Invalid,
            activeRevision,
            null,
            errors);
    }

    private static RunOnceExecutionResult ExecutionResult(
        RunOnceExecutionOutcome outcome,
        long activeRevision)
    {
        return new RunOnceExecutionResult(
            outcome,
            activeRevision,
            null,
            []);
    }

    private sealed class OperationValidation
    {
        private OperationValidation(
            MappingConfiguration? activeOperationalConfiguration,
            MappingConfiguration? operationConfiguration,
            RunOnceOperation? operation,
            IEnumerable<Guid> excludedItemIds,
            IEnumerable<RunOnceValidationError> errors)
        {
            ActiveOperationalConfiguration = activeOperationalConfiguration!;
            OperationConfiguration = operationConfiguration!;
            Operation = operation;
            ExcludedItemIds = new HashSet<Guid>(excludedItemIds);
            Errors = [.. errors];
        }

        public MappingConfiguration ActiveOperationalConfiguration { get; }

        public MappingConfiguration OperationConfiguration { get; }

        public RunOnceOperation? Operation { get; }

        public HashSet<Guid> ExcludedItemIds { get; }

        public IReadOnlyList<RunOnceValidationError> Errors { get; }

        public static OperationValidation Invalid(RunOnceValidationError error)
        {
            return Invalid([error]);
        }

        public static OperationValidation Invalid(IEnumerable<RunOnceValidationError> errors)
        {
            return new OperationValidation(null, null, null, [], errors);
        }

        public static OperationValidation Valid(
            MappingConfiguration activeOperationalConfiguration,
            MappingConfiguration operationConfiguration,
            RunOnceOperation operation,
            IEnumerable<Guid> excludedItemIds)
        {
            return new OperationValidation(
                activeOperationalConfiguration,
                operationConfiguration,
                operation,
                excludedItemIds,
                []);
        }
    }

    private sealed class CandidatePlan
    {
        private CandidatePlan(
            int totalItemCount,
            IEnumerable<Guid> itemIds,
            IEnumerable<ReconciliationPlan> plans,
            IEnumerable<Guid> excludableItemIds,
            IEnumerable<RunOnceValidationError> errors)
        {
            TotalItemCount = totalItemCount;
            ItemIds = [.. itemIds];
            Plans = [.. plans];
            ExcludableItemIds = [.. excludableItemIds];
            Errors = [.. errors];
        }

        public int TotalItemCount { get; }

        public IReadOnlyList<Guid> ItemIds { get; }

        public IReadOnlyList<ReconciliationPlan> Plans { get; }

        public IReadOnlyList<Guid> ExcludableItemIds { get; }

        public IReadOnlyList<RunOnceValidationError> Errors { get; }

        public static CandidatePlan Invalid(
            int totalItemCount,
            IEnumerable<RunOnceValidationError> errors)
        {
            return new CandidatePlan(totalItemCount, [], [], [], errors);
        }

        public static CandidatePlan Valid(
            int totalItemCount,
            IEnumerable<Guid> itemIds,
            IEnumerable<ReconciliationPlan> plans,
            IEnumerable<Guid> excludableItemIds)
        {
            return new CandidatePlan(totalItemCount, itemIds, plans, excludableItemIds, []);
        }
    }
}
