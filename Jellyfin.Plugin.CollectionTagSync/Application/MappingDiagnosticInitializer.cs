using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Rehydrates operational mapping diagnostics from persisted configuration at startup.
/// </summary>
internal sealed class MappingDiagnosticInitializer : IHostedService
{
    private readonly IActiveMappingProvider _mappingProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MappingDiagnosticInitializer"/> class.
    /// </summary>
    /// <param name="mappingProvider">The active mapping provider.</param>
    public MappingDiagnosticInitializer(IActiveMappingProvider mappingProvider)
    {
        _mappingProvider = mappingProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _mappingProvider.GetConfiguration();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
