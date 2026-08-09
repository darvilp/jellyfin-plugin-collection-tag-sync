using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.CollectionTagSync.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class AdministratorUiControllerContractTests
{
    [Theory]
    [InlineData(typeof(TagsController), "CollectionTagSync/Tags")]
    [InlineData(typeof(OperationalStatusController), "CollectionTagSync/Status")]
    public void SupportingUiControllersRequireAdministratorElevation(
        System.Type controllerType,
        string expectedRoute)
    {
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        var route = Assert.Single(controllerType.GetCustomAttributes<RouteAttribute>());

        Assert.Equal(Policies.RequiresElevation, authorize.Policy);
        Assert.Equal(expectedRoute, route.Template);
        Assert.NotNull(controllerType.GetCustomAttribute<ApiControllerAttribute>());
    }

    [Fact]
    public void FullReconcileExposesExplicitBackgroundQueueAction()
    {
        var method = Assert.Single(
            typeof(FullReconcileController).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            candidate => candidate.Name == "QueueManual");

        Assert.Equal(string.Empty, method.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotEmpty(method.GetCustomAttributes<ProducesResponseTypeAttribute>());
    }
}
