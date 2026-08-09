using System.Collections.Generic;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Returns a created selected value or explicit validation recovery choices.
/// </summary>
public sealed class CollectionCreationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionCreationResult"/> class.
    /// </summary>
    /// <param name="outcome">The creation outcome.</param>
    /// <param name="selectedCollection">The newly created selected value.</param>
    /// <param name="matchingCollections">Existing duplicate-name picker choices.</param>
    /// <param name="message">The administrator-facing result explanation.</param>
    internal CollectionCreationResult(
        CollectionCreationOutcome outcome,
        CollectionPickerEntry? selectedCollection,
        IEnumerable<CollectionPickerEntry> matchingCollections,
        string message)
    {
        Outcome = outcome;
        SelectedCollection = selectedCollection;
        MatchingCollections = [.. matchingCollections];
        Message = message;
    }

    /// <summary>Gets the creation outcome.</summary>
    public CollectionCreationOutcome Outcome { get; }

    /// <summary>Gets the newly created selected value, when successful.</summary>
    public CollectionPickerEntry? SelectedCollection { get; }

    /// <summary>Gets existing duplicate-name entries for explicit picker recovery.</summary>
    public IReadOnlyList<CollectionPickerEntry> MatchingCollections { get; }

    /// <summary>Gets the administrator-facing result explanation.</summary>
    public string Message { get; }
}
