using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;

namespace ProdFalcon.Infrastructure.Background;

public class StorageCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StorageCleanupHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);
    private readonly TimeSpan _maxAge = TimeSpan.FromDays(2);

    public StorageCleanupHostedService(IServiceProvider serviceProvider, ILogger<StorageCleanupHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Storage cleanup job failed");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal on host shutdown (includes TaskCanceledException).
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IProjectStorageService>();
        var root = storage.StorageRoot;

        if (!Directory.Exists(root))
            return;

        foreach (var dir in Directory.GetDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new DirectoryInfo(dir);
            if (info.LastWriteTimeUtc < DateTime.UtcNow.Subtract(_maxAge)
                && Guid.TryParse(Path.GetFileName(dir), out var projectId))
            {
                await storage.CleanupProjectAsync(projectId, cancellationToken);
                _logger.LogInformation("Cleaned up stale project storage {ProjectId}", projectId);
            }
        }
    }
}
