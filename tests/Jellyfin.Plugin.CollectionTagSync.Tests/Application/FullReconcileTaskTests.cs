using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CollectionTagSync.Application;
using Jellyfin.Plugin.CollectionTagSync.Tasks;
using MediaBrowser.Model.Tasks;
using Xunit;

namespace Jellyfin.Plugin.CollectionTagSync.Tests.Application;

public sealed class FullReconcileTaskTests
{
    [Fact]
    public void TaskIsVisibleEnabledLoggedAndManualByDefault()
    {
        var task = new FullReconcileTask(new FullReconcileRequestStore());

        Assert.IsAssignableFrom<IScheduledTask>(task);
        var configurable = Assert.IsAssignableFrom<IConfigurableScheduledTask>(task);
        Assert.False(configurable.IsHidden);
        Assert.True(configurable.IsEnabled);
        Assert.True(configurable.IsLogged);
        Assert.Empty(task.GetDefaultTriggers());
        Assert.Equal("CollectionTagSyncFullReconcile", task.Key);
    }

    [Fact]
    public async Task ExecutionRequestsManualReconcileAndWaitsForItsTerminalResult()
    {
        var requestStore = new FullReconcileRequestStore();
        var task = new FullReconcileTask(requestStore);
        var progress = new RecordingProgress();

        var execution = task.ExecuteAsync(progress, CancellationToken.None);
        Assert.True(requestStore.TryClaim(out var request));
        Assert.Equal([FullReconcileRequestReason.Manual], request.Reasons);
        Assert.False(execution.IsCompleted);

        request.Complete(Completed(request));
        await execution.ConfigureAwait(true);

        Assert.Collection(
            progress.Values,
            value => Assert.Equal(0D, value),
            value => Assert.Equal(100D, value));
    }

    [Fact]
    public async Task RunWideFailureMarksTheJellyfinTaskFailed()
    {
        var requestStore = new FullReconcileRequestStore();
        var task = new FullReconcileTask(requestStore);
        var execution = task.ExecuteAsync(new RecordingProgress(), CancellationToken.None);
        Assert.True(requestStore.TryClaim(out var request));
        request.Complete(new FullReconcileRunResult(
            request.Id,
            FullReconcileState.Failed,
            request.Reasons,
            totalItemCount: 0,
            succeededItemCount: 0,
            failedItemCount: 0));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => execution).ConfigureAwait(true);

        Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FullReconcileRunResult Completed(FullReconcileRequest request)
    {
        return new FullReconcileRunResult(
            request.Id,
            FullReconcileState.Completed,
            request.Reasons,
            totalItemCount: 0,
            succeededItemCount: 0,
            failedItemCount: 0);
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value)
        {
            Values.Add(value);
        }
    }
}
