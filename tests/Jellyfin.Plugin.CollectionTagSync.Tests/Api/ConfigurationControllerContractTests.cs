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
    public void ActivationAndStatusActionsHaveExplicitRoutes()
    {
        var methods = typeof(ConfigurationController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var activate = Assert.Single(methods, method => method.Name == "ActivateAsync");
        var status = Assert.Single(methods, method => method.Name == "GetReconciliationStatus");

        Assert.NotNull(activate.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(status.GetCustomAttribute<HttpGetAttribute>());
    }
}
