using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.CollectionTagSync.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class FullReconcileControllerContractTests
{
    [Fact]
    public void ControllerRequiresAdministratorElevation()
    {
        var authorize = Assert.Single(
            typeof(FullReconcileController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(Policies.RequiresElevation, authorize.Policy);
        Assert.NotNull(typeof(FullReconcileController).GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void StatusPreviewAndConfirmationActionsHaveExplicitRoutes()
    {
        var methods = typeof(FullReconcileController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var status = Assert.Single(methods, method => method.Name == "GetStatus");
        var preview = Assert.Single(methods, method => method.Name == "CreatePreviewAuthorization");
        var confirm = Assert.Single(methods, method => method.Name == "ConfirmAsync");

        Assert.Equal("Status", status.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(
            "{runId:guid}/Preview",
            preview.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal(
            "{runId:guid}/Confirm",
            confirm.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.All(
            new[] { status, preview, confirm },
            method => Assert.NotEmpty(method.GetCustomAttributes<ProducesResponseTypeAttribute>()));
    }
}
