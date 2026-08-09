using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CollectionTagSync;

/// <summary>
/// Observes the Jellyfin events that will feed reconciliation in later phases.
/// </summary>
internal sealed partial class JellyfinEventObserver : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly ILogger<JellyfinEventObserver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinEventObserver"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="collectionManager">The Jellyfin collection manager.</param>
    /// <param name="logger">The logger.</param>
    public JellyfinEventObserver(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ILogger<JellyfinEventObserver> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnItemUpdated;
        _collectionManager.CollectionCreated += OnCollectionCreated;
        _collectionManager.ItemsAddedToCollection += OnItemsAddedToCollection;
        _collectionManager.ItemsRemovedFromCollection += OnItemsRemovedFromCollection;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _collectionManager.CollectionCreated -= OnCollectionCreated;
        _collectionManager.ItemsAddedToCollection -= OnItemsAddedToCollection;
        _collectionManager.ItemsRemovedFromCollection -= OnItemsRemovedFromCollection;
        return Task.CompletedTask;
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs eventArgs)
    {
        LogItemUpdated(
            _logger,
            eventArgs.Item.Id,
            eventArgs.Item.GetType().Name,
            eventArgs.UpdateReason);
    }

    private void OnCollectionCreated(object? sender, CollectionCreatedEventArgs eventArgs)
    {
        LogCollectionCreated(_logger, eventArgs.Collection.Id);
    }

    private void OnItemsAddedToCollection(object? sender, CollectionModifiedEventArgs eventArgs)
    {
        LogItemsAddedToCollection(
            _logger,
            eventArgs.Collection.Id,
            eventArgs.ItemsChanged.Count);
    }

    private void OnItemsRemovedFromCollection(object? sender, CollectionModifiedEventArgs eventArgs)
    {
        LogItemsRemovedFromCollection(
            _logger,
            eventArgs.Collection.Id,
            eventArgs.ItemsChanged.Count);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync event: ItemUpdated ItemId={ItemId} ItemType={ItemType} Reason={UpdateReason}")]
    private static partial void LogItemUpdated(
        ILogger logger,
        Guid itemId,
        string itemType,
        ItemUpdateType updateReason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync event: CollectionCreated CollectionId={CollectionId}")]
    private static partial void LogCollectionCreated(ILogger logger, Guid collectionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync event: ItemsAddedToCollection CollectionId={CollectionId} ItemCount={ItemCount}")]
    private static partial void LogItemsAddedToCollection(ILogger logger, Guid collectionId, int itemCount);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Collection Tag Sync event: ItemsRemovedFromCollection CollectionId={CollectionId} ItemCount={ItemCount}")]
    private static partial void LogItemsRemovedFromCollection(ILogger logger, Guid collectionId, int itemCount);
}
