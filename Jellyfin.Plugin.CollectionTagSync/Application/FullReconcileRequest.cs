using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Carries one claimed coalesced Full Reconcile request.
/// </summary>
internal sealed class FullReconcileRequest
{
    private readonly TaskCompletionSource<FullReconcileRunResult> _completion;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileRequest"/> class.
    /// </summary>
    public FullReconcileRequest()
    {
        Id = Guid.NewGuid();
        Reasons = Array.AsReadOnly<FullReconcileRequestReason>([]);
        _completion = new TaskCompletionSource<FullReconcileRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FullReconcileRequest"/> class for a consumed authorization.
    /// </summary>
    /// <param name="confirmation">The consumed in-process confirmation.</param>
    public FullReconcileRequest(FullReconcileConfirmation confirmation)
        : this()
    {
        Confirmation = confirmation;
        Reasons = Array.AsReadOnly([FullReconcileRequestReason.Manual]);
    }

    private FullReconcileRequest(
        Guid id,
        IEnumerable<FullReconcileRequestReason> reasons,
        FullReconcileConfirmation? confirmation,
        TaskCompletionSource<FullReconcileRunResult> completion)
    {
        Id = id;
        Reasons = Array.AsReadOnly([.. reasons]);
        Confirmation = confirmation;
        _completion = completion;
    }

    /// <summary>
    /// Gets the opaque run identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the reasons coalesced into this run.
    /// </summary>
    public IReadOnlyList<FullReconcileRequestReason> Reasons { get; }

    /// <summary>Gets the consumed authorization carried by a confirmation run.</summary>
    public FullReconcileConfirmation? Confirmation { get; }

    /// <summary>
    /// Gets the shared completion observed by all requesters in this batch.
    /// </summary>
    public Task<FullReconcileRunResult> Completion => _completion.Task;

    /// <summary>
    /// Returns the claimed request with its final reason snapshot.
    /// </summary>
    /// <param name="reasons">The final coalesced reasons.</param>
    /// <returns>The claimed request.</returns>
    public FullReconcileRequest WithReasons(IEnumerable<FullReconcileRequestReason> reasons)
    {
        return new FullReconcileRequest(Id, reasons, Confirmation, _completion);
    }

    /// <summary>
    /// Completes every waiter with one terminal result.
    /// </summary>
    /// <param name="result">The terminal run result.</param>
    public void Complete(FullReconcileRunResult result)
    {
        _completion.TrySetResult(result);
    }
}
