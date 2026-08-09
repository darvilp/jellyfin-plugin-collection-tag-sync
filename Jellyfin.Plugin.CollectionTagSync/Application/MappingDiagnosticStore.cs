using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Retains current unresolved-group warnings for status and UI consumers.
/// </summary>
public sealed class MappingDiagnosticStore
{
    private readonly object _sync = new();
    private IReadOnlyList<UnresolvedMappingGroupDiagnostic> _unresolvedGroups =
        Array.Empty<UnresolvedMappingGroupDiagnostic>();

    /// <summary>
    /// Gets an immutable snapshot of unresolved enabled groups.
    /// </summary>
    public IReadOnlyList<UnresolvedMappingGroupDiagnostic> UnresolvedGroups
    {
        get
        {
            lock (_sync)
            {
                return _unresolvedGroups;
            }
        }
    }

    /// <summary>
    /// Replaces the current operational warning snapshot.
    /// </summary>
    /// <param name="unresolvedGroups">The current unresolved groups.</param>
    /// <returns><see langword="true"/> when the diagnostic snapshot changed.</returns>
    internal bool Update(IEnumerable<UnresolvedMappingGroupDiagnostic> unresolvedGroups)
    {
        ArgumentNullException.ThrowIfNull(unresolvedGroups);

        ReadOnlyCollection<UnresolvedMappingGroupDiagnostic> replacement =
            Array.AsReadOnly([.. unresolvedGroups]);
        lock (_sync)
        {
            if (AreEquivalent(_unresolvedGroups, replacement))
            {
                return false;
            }

            _unresolvedGroups = replacement;
            return true;
        }
    }

    private static bool AreEquivalent(
        IReadOnlyList<UnresolvedMappingGroupDiagnostic> left,
        ReadOnlyCollection<UnresolvedMappingGroupDiagnostic> right)
    {
        return left.Count == right.Count
            && left.Zip(right).All(pair =>
                pair.First.GroupIndex == pair.Second.GroupIndex
                && pair.First.Target.Equals(pair.Second.Target)
                && StringComparer.Ordinal.Equals(
                    pair.First.Target.DisplayLabel,
                    pair.Second.Target.DisplayLabel)
                && pair.First.MissingCollections.Count == pair.Second.MissingCollections.Count
                && pair.First.MissingCollections.Zip(pair.Second.MissingCollections).All(collectionPair =>
                    collectionPair.First.Equals(collectionPair.Second)
                    && StringComparer.Ordinal.Equals(
                        collectionPair.First.DisplayLabel,
                        collectionPair.Second.DisplayLabel)));
    }
}
