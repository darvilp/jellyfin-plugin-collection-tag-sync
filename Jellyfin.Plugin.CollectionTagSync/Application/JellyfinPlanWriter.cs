using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Domain;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Applies direct tag and collection-membership plans through Jellyfin services.
/// </summary>
internal sealed class JellyfinPlanWriter : IPlanWriter
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinPlanWriter"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="collectionManager">The Jellyfin collection manager.</param>
    public JellyfinPlanWriter(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ReconciliationPlan plan, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(plan.ItemId)
            ?? throw new InvalidOperationException($"Jellyfin item {plan.ItemId:D} no longer exists.");

        foreach (var mutation in plan.Mutations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (mutation.Kind)
            {
                case PlannedMutationKind.AddTag:
                    await AddTagAsync(item, mutation, cancellationToken).ConfigureAwait(false);
                    break;
                case PlannedMutationKind.RemoveTag:
                    await RemoveTagAsync(item, mutation, cancellationToken).ConfigureAwait(false);
                    break;
                case PlannedMutationKind.AddCollectionMembership:
                    await _collectionManager
                        .AddToCollectionAsync(GetCollectionId(mutation), [plan.ItemId])
                        .ConfigureAwait(false);
                    break;
                case PlannedMutationKind.RemoveCollectionMembership:
                    await _collectionManager
                        .RemoveFromCollectionAsync(GetCollectionId(mutation), [plan.ItemId])
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("Unknown planned mutation kind.");
            }
        }
    }

    private static Guid GetCollectionId(PlannedMutation mutation)
    {
        return mutation.Target is CollectionNode collection
            ? collection.Id
            : throw new InvalidOperationException("A collection mutation requires a collection target.");
    }

    private async Task AddTagAsync(
        BaseItem item,
        PlannedMutation mutation,
        CancellationToken cancellationToken)
    {
        var target = mutation.Target as TagNode
            ?? throw new InvalidOperationException("A tag mutation requires a tag target.");
        if (item.Tags.Any(value =>
                StringComparer.OrdinalIgnoreCase.Equals(value.Trim(), target.Value)))
        {
            return;
        }

        var configuredValue = mutation.TagValues.Single();
        item.Tags = [.. item.Tags, configuredValue];
        await UpdateTagsAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveTagAsync(
        BaseItem item,
        PlannedMutation mutation,
        CancellationToken cancellationToken)
    {
        var target = mutation.Target as TagNode
            ?? throw new InvalidOperationException("A tag mutation requires a tag target.");
        var retainedTags = item.Tags
            .Where(value => !StringComparer.OrdinalIgnoreCase.Equals(value.Trim(), target.Value))
            .ToArray();
        if (retainedTags.Length == item.Tags.Length)
        {
            return;
        }

        item.Tags = retainedTags;
        await UpdateTagsAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private Task UpdateTagsAsync(BaseItem item, CancellationToken cancellationToken)
    {
        return _libraryManager.UpdateItemAsync(
            item,
            item.GetParent(),
            ItemUpdateType.MetadataEdit,
            cancellationToken);
    }
}
