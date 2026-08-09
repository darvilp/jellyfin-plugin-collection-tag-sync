using System.Collections.Generic;
using Jellyfin.Plugin.CollectionTagSync.Api;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Api;

public sealed class AdministratorUiControllerTests
{
    [Fact]
    public void TagPickerReturnsOnlyCatalogValues()
    {
        var catalog = new Mock<ITagCatalog>(MockBehavior.Strict);
        catalog.Setup(value => value.GetPickerEntries()).Returns(["Kid-Approved", "Waltney"]);
        var controller = new TagsController(catalog.Object);

        var response = Assert.IsType<OkObjectResult>(controller.GetPickerEntries().Result);

        var values = Assert.IsAssignableFrom<IReadOnlyList<string>>(response.Value);
        Assert.Collection(
            values,
            value => Assert.Equal("Kid-Approved", value),
            value => Assert.Equal("Waltney", value));
        catalog.VerifyAll();
    }

    [Fact]
    public void OperationalStatusReturnsPrivacySafeCoordinatorAndDiagnosticSnapshot()
    {
        var incremental = new Mock<IIncrementalReconciliationControl>(MockBehavior.Strict);
        incremental.SetupGet(value => value.Status).Returns(
            new IncrementalReconciliationStatus(queuedItemCount: 2, runningItemCount: 1, quarantinedItemCount: 3, isStormFallbackActive: true));
        var controller = new OperationalStatusController(
            incremental.Object,
            new FullReconcileRequestStore(),
            new MappingDiagnosticStore());

        var response = Assert.IsType<OkObjectResult>(controller.GetStatus().Result);
        var status = Assert.IsType<OperationalStatusResult>(response.Value);

        Assert.Equal(2, status.Incremental.QueuedItemCount);
        Assert.Equal(1, status.Incremental.RunningItemCount);
        Assert.Equal(3, status.Incremental.QuarantinedItemCount);
        Assert.True(status.Incremental.IsStormFallbackActive);
        Assert.False(status.FullReconcileRequest.IsRequested);
        Assert.Empty(status.UnresolvedGroups);
        incremental.VerifyAll();
    }

    [Fact]
    public void FullReconcileQueueActionRequestsBackgroundManualRun()
    {
        var requestStore = new FullReconcileRequestStore();
        var controller = new FullReconcileController(null!, null!, null!, requestStore);

        var response = Assert.IsType<AcceptedResult>(controller.QueueManual().Result);
        var status = Assert.IsType<FullReconcileRequestStatus>(response.Value);

        Assert.True(status.IsRequested);
        Assert.Equal(FullReconcileRequestReason.Manual, Assert.Single(status.Reasons));
    }
}
