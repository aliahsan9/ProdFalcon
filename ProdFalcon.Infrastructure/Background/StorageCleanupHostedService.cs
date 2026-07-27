using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                    Cleanup(stoppingToken);
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

    private void Cleanup(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IProjectStorageService>();
        var root = storage.StorageRoot;

        if (!Directory.Exists(root))
            return;

        foreach (var tenantDir in Directory.GetDirectories(root))
        {
            foreach (var projectDir in Directory.GetDirectories(tenantDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var info = new DirectoryInfo(projectDir);
                if (info.LastWriteTimeUtc >= DateTime.UtcNow.Subtract(_maxAge))
                    continue;

                if (!Guid.TryParse(Path.GetFileName(projectDir), out _))
                    continue;

                Directory.Delete(projectDir, recursive: true);
                _logger.LogInformation(
                    "Cleaned up stale project storage {ProjectDir} under tenant {TenantDir}",
                    Path.GetFileName(projectDir),
                    Path.GetFileName(tenantDir));
            }
        }
    }
}
