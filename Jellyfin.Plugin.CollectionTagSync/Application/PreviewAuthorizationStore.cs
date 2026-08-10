using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Stores restart-scoped, expiring, administrator-bound single-use authorizations.
/// </summary>
/// <typeparam name="TPayload">The immutable authorization payload type.</typeparam>
internal sealed class PreviewAuthorizationStore<TPayload>
    where TPayload : class
{
    private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(10);
    private readonly object _sync = new();
    private readonly Dictionary<string, AuthorizationEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewAuthorizationStore{TPayload}"/> class.
    /// </summary>
    /// <param name="timeProvider">The authorization clock.</param>
    public PreviewAuthorizationStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Issues one opaque authorization.</summary>
    /// <param name="administratorId">The initiating administrator identity.</param>
    /// <param name="payload">The immutable authorization payload.</param>
    /// <returns>The opaque authorization and expiry.</returns>
    public (string Authorization, DateTimeOffset ExpiresAtUtc) Issue(
        Guid administratorId,
        TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (administratorId == Guid.Empty)
        {
            throw new ArgumentException("An administrator identity is required.", nameof(administratorId));
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            foreach (var expired in _entries
                .Where(pair => pair.Value.ExpiresAtUtc <= now)
                .Select(pair => pair.Key)
                .ToArray())
            {
                _entries.Remove(expired);
            }

            var authorization = Guid.NewGuid().ToString("N");
            var expiresAtUtc = now.Add(AuthorizationLifetime);
            _entries.Add(
                authorization,
                new AuthorizationEntry(administratorId, expiresAtUtc, payload));
            return (authorization, expiresAtUtc);
        }
    }

    /// <summary>Consumes one valid authorization.</summary>
    /// <param name="administratorId">The confirming administrator identity.</param>
    /// <param name="authorization">The opaque authorization.</param>
    /// <param name="matches">An optional payload binding predicate evaluated before consumption.</param>
    /// <returns>The immutable payload, or <see langword="null"/> when invalid.</returns>
    public TPayload? Consume(
        Guid administratorId,
        string authorization,
        Predicate<TPayload>? matches = null)
    {
        if (administratorId == Guid.Empty || string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        lock (_sync)
        {
            if (!_entries.TryGetValue(authorization, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAtUtc <= _timeProvider.GetUtcNow())
            {
                _entries.Remove(authorization);
                return null;
            }

            if (entry.AdministratorId != administratorId
                || (matches is not null && !matches(entry.Payload)))
            {
                return null;
            }

            _entries.Remove(authorization);
            return entry.Payload;
        }
    }

    /// <summary>Invalidates every outstanding authorization.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
        }
    }

    /// <summary>Invalidates outstanding authorizations whose payload matches a predicate.</summary>
    /// <param name="matches">The payload predicate.</param>
    public void RemoveWhere(Predicate<TPayload> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        lock (_sync)
        {
            foreach (var key in _entries
                .Where(pair => matches(pair.Value.Payload))
                .Select(pair => pair.Key)
                .ToArray())
            {
                _entries.Remove(key);
            }
        }
    }

    private sealed record AuthorizationEntry(
        Guid AdministratorId,
        DateTimeOffset ExpiresAtUtc,
        TPayload Payload);
}
