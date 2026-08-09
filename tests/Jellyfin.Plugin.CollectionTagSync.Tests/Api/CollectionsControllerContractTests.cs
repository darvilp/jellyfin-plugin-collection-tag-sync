using System.Reflection;
using Jellyfin.Plugin.CollectionTagSync.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class CollectionsControllerContractTests
{
    [Fact]
    public void ControllerRequiresAdministratorElevation()
    {
        var authorize = Assert.Single(
            typeof(CollectionsController).GetCustomAttributes<AuthorizeAttribute>());
        var route = Assert.Single(
            typeof(CollectionsController).GetCustomAttributes<RouteAttribute>());

        Assert.Equal(Policies.RequiresElevation, authorize.Policy);
        Assert.Equal("CollectionTagSync/Collections", route.Template);
        Assert.NotNull(typeof(CollectionsController).GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void PickerAndAddNewActionsHaveDistinctExplicitRoutes()
    {
        var methods = typeof(CollectionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var picker = Assert.Single(methods, method => method.Name == "GetPickerEntries");
        var create = Assert.Single(methods, method => method.Name == "CreateAsync");

        Assert.Equal("Picker", picker.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("Create", create.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }
}
