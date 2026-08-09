using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Provides GUID-only picker data and serialized independent collection creation.
/// </summary>
public sealed class CollectionSelectionService : IDisposable
{
    private readonly SemaphoreSlim _creationLock = new(1, 1);
    private readonly ICollectionCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionSelectionService"/> class.
    /// </summary>
    /// <param name="catalog">The Jellyfin collection catalog.</param>
    public CollectionSelectionService(ICollectionCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Gets every current GUID-backed picker entry.</summary>
    /// <returns>Deterministically ordered current collection choices.</returns>
    public IReadOnlyList<CollectionPickerEntry> GetPickerEntries()
    {
        return Order(_catalog.GetPickerEntries());
    }

    /// <summary>
    /// Validates and immediately creates one independent Jellyfin collection.
    /// </summary>
    /// <param name="displayName">The proposed display name.</param>
    /// <param name="cancellationToken">Cancellation before the independent create begins.</param>
    /// <returns>The selected created value or duplicate-name recovery entries.</returns>
    public async Task<CollectionCreationResult> CreateAsync(
        string? displayName,
        CancellationToken cancellationToken)
    {
        var normalizedName = displayName?.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            return new CollectionCreationResult(
                CollectionCreationOutcome.InvalidName,
                null,
                [],
                "A new collection name must contain at least one non-whitespace character.");
        }

        await _creationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matches = Order(_catalog.GetPickerEntries()
                .Where(entry => StringComparer.OrdinalIgnoreCase.Equals(
                    entry.DisplayName.Trim(),
                    normalizedName)));
            if (matches.Length > 0)
            {
                return new CollectionCreationResult(
                    CollectionCreationOutcome.DuplicateName,
                    null,
                    matches,
                    "An existing collection already uses that normalized display name. Select it explicitly by GUID.");
            }

            var created = await _catalog.CreateAsync(normalizedName).ConfigureAwait(false);
            return new CollectionCreationResult(
                CollectionCreationOutcome.Created,
                created,
                [],
                "The collection was created independently and is now the selected value.");
        }
        finally
        {
            _creationLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _creationLock.Dispose();
    }

    private static CollectionPickerEntry[] Order(
        IEnumerable<CollectionPickerEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Id)
            .ToArray();
    }
}
