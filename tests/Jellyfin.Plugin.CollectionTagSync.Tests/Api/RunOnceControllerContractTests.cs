using System.Reflection;
using Jellyfin.Plugin.CollectionTagSync.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class RunOnceControllerContractTests
{
    [Fact]
    public void ControllerRequiresAdministratorElevation()
    {
        var authorize = Assert.Single(
            typeof(RunOnceController).GetCustomAttributes<AuthorizeAttribute>());
        var route = Assert.Single(
            typeof(RunOnceController).GetCustomAttributes<RouteAttribute>());

        Assert.Equal(Policies.RequiresElevation, authorize.Policy);
        Assert.Equal("CollectionTagSync/RunOnce", route.Template);
        Assert.NotNull(typeof(RunOnceController).GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void PreviewConfirmationAndStatusActionsHaveExplicitRoutes()
    {
        var methods = typeof(RunOnceController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var preview = Assert.Single(methods, method => method.Name == "PreviewAsync");
        var confirm = Assert.Single(methods, method => method.Name == "ConfirmAsync");
        var status = Assert.Single(methods, method => method.Name == "GetReconciliationStatus");

        Assert.Equal("Preview", preview.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal("Confirm", confirm.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal(
            "Reconciliations/{id:guid}",
            status.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }
}
