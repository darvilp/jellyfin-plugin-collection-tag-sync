using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Coalesces reasons that require a future serialized Full Reconcile.
/// </summary>
public sealed class FullReconcileRequestStore
{
    private readonly object _sync = new();
    private readonly HashSet<FullReconcileRequestReason> _reasons = [];
    private readonly Channel<bool> _signals = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private FullReconcileRequest? _pendingRequest;

    /// <summary>
    /// Gets the current coalesced request status.
    /// </summary>
    public FullReconcileRequestStatus Status
    {
        get
        {
            lock (_sync)
            {
                return new FullReconcileRequestStatus(_reasons.Order());
            }
        }
    }

    /// <summary>
    /// Gets the single-consumer signal reader.
    /// </summary>
    internal ChannelReader<bool> SignalReader => _signals.Reader;

    /// <summary>
    /// Adds one reason to the coalesced request.
    /// </summary>
    /// <param name="reason">The request reason.</param>
    internal void Request(FullReconcileRequestReason reason)
    {
        _ = RequestCore(reason);
    }

    /// <summary>
    /// Adds one reason and waits for the coalesced run that owns it.
    /// </summary>
    /// <param name="reason">The request reason.</param>
    /// <param name="cancellationToken">Cancellation for this waiter only.</param>
    /// <returns>The terminal run result.</returns>
    internal Task<FullReconcileRunResult> RequestAsync(
        FullReconcileRequestReason reason,
        CancellationToken cancellationToken)
    {
        return RequestCore(reason).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Claims the current coalesced request for serialized execution.
    /// </summary>
    /// <param name="request">The claimed request.</param>
    /// <returns><see langword="true"/> when a request was available.</returns>
    internal bool TryClaim(out FullReconcileRequest request)
    {
        lock (_sync)
        {
            if (_pendingRequest is null)
            {
                request = null!;
                return false;
            }

            request = _pendingRequest.WithReasons(_reasons.Order());
            _pendingRequest = null;
            _reasons.Clear();
            return true;
        }
    }

    private Task<FullReconcileRunResult> RequestCore(FullReconcileRequestReason reason)
    {
        lock (_sync)
        {
            _reasons.Add(reason);
            if (_pendingRequest is null)
            {
                _pendingRequest = new FullReconcileRequest();
                if (!_signals.Writer.TryWrite(true))
                {
                    throw new System.InvalidOperationException("The Full Reconcile dispatcher is unavailable.");
                }
            }

            return _pendingRequest.Completion;
        }
    }
}
