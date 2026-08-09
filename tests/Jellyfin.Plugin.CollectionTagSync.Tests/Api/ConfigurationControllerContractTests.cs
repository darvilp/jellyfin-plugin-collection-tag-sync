using System.Reflection;
using Jellyfin.Plugin.CollectionTagSync.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class ConfigurationControllerContractTests
{
    [Fact]
    public void ControllerRequiresAdministratorElevation()
    {
        var authorize = Assert.Single(
            typeof(ConfigurationController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(Policies.RequiresElevation, authorize.Policy);
        Assert.NotNull(typeof(ConfigurationController).GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void ActivationPreviewConfirmationAndStatusActionsHaveExplicitRoutes()
    {
        var methods = typeof(ConfigurationController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var activate = Assert.Single(methods, method => method.Name == "ActivateAsync");
        var preview = Assert.Single(methods, method => method.Name == "PreviewAsync");
        var confirm = Assert.Single(methods, method => method.Name == "ConfirmAsync");
        var status = Assert.Single(methods, method => method.Name == "GetReconciliationStatus");

        Assert.NotNull(activate.GetCustomAttribute<HttpPostAttribute>());
        Assert.Equal("Preview", preview.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal("Confirm", confirm.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(status.GetCustomAttribute<HttpGetAttribute>());
    }
}
