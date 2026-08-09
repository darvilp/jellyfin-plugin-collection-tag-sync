using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CollectionTagSync.Application;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

internal sealed class TestItemTitleProvider : IItemTitleProvider
{
    private readonly IReadOnlyDictionary<Guid, string> _titles;

    public TestItemTitleProvider(params (Guid ItemId, string Title)[] titles)
    {
        _titles = titles.ToDictionary(title => title.ItemId, title => title.Title);
    }

    public string GetTitle(Guid itemId)
    {
        return _titles.GetValueOrDefault(itemId, string.Empty);
    }
}
