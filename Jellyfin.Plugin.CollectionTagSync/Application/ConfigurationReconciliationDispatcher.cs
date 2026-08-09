using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Dispatches accepted configuration reconciliation before HTTP responses return.
/// </summary>
public sealed class ConfigurationReconciliationDispatcher
{
    private readonly Channel<ConfigurationReconciliationRequest> _requests =
        Channel.CreateUnbounded<ConfigurationReconciliationRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly BackgroundReconciliationStatusStore _statusStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationReconciliationDispatcher"/> class.
    /// </summary>
    /// <param name="statusStore">The background status store.</param>
    public ConfigurationReconciliationDispatcher(BackgroundReconciliationStatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    /// <summary>
    /// Gets the single-consumer request reader.
    /// </summary>
    internal ChannelReader<ConfigurationReconciliationRequest> Reader => _requests.Reader;

    /// <summary>
    /// Enqueues one accepted configuration reconciliation.
    /// </summary>
    /// <param name="revision">The accepted configuration revision.</param>
    /// <param name="itemIds">The complete eligible item identity set.</param>
    /// <param name="configuration">The immutable accepted configuration.</param>
    /// <returns>The opaque background reconciliation identity.</returns>
    internal Guid Enqueue(
        long revision,
        IReadOnlyList<Guid> itemIds,
        MappingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        ArgumentNullException.ThrowIfNull(configuration);

        var id = _statusStore.CreateQueued(revision, itemIds.Count);
        if (!_requests.Writer.TryWrite(new ConfigurationReconciliationRequest(
                id,
                revision,
                itemIds,
                configuration)))
        {
            throw new InvalidOperationException(
                "The configuration reconciliation dispatcher is unavailable.");
        }

        return id;
    }
}
