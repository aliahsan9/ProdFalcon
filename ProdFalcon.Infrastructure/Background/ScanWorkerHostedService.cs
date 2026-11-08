using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Domain.Enums;

namespace ProdFalcon.Infrastructure.Background;

public class ScanWorkerHostedService : BackgroundService
{
    private readonly IScanQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanWorkerHostedService> _logger;
    private readonly ScanLimitsOptions _limits;

    public ScanWorkerHostedService(
        IScanQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<ScanLimitsOptions> limits,
        ILogger<ScanWorkerHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _limits = limits.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, 2).Select(_ => ConsumeAsync(stoppingToken));
        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(Math.Max(1, _limits.ScanTimeoutMinutes)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token, _queue.CancellationFor(item.ScanResultId));
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                tenant.SetTenant(item.TenantId, item.UserId, role: TenantRole.Owner);
                var scans = scope.ServiceProvider.GetRequiredService<IScanService>();
                await scans.ExecuteQueuedScanAsync(item.ProjectId, item.ScanResultId, null, item.GitHubAccessToken, linked.Token);
                _logger.LogInformation("Scan {ScanResultId} completed for tenant {TenantId}", item.ScanResultId, item.TenantId);
            }
            catch (OperationCanceledException)
            {
                var userCancelled = false;
                try
                {
                    userCancelled = _queue.CancellationFor(item.ScanResultId).IsCancellationRequested;
                }
                catch (ObjectDisposedException)
                {
                    userCancelled = false;
                }

                if (timeout.IsCancellationRequested && !userCancelled && !stoppingToken.IsCancellationRequested)
                {
                    await MarkTimedOutAsync(item);
                    _logger.LogInformation("Scan {ScanResultId} exceeded the configured time limit", item.ScanResultId);
                }
                else
                {
                    _logger.LogInformation("Scan {ScanResultId} was cancelled", item.ScanResultId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scan {ScanResultId} failed", item.ScanResultId);
            }
            finally
            {
                _queue.Forget(item.ScanResultId);
            }
        }
    }

    private async Task MarkTimedOutAsync(ScanWorkItem item)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenant(item.TenantId, item.UserId, role: TenantRole.Owner);
            var results = scope.ServiceProvider.GetRequiredService<IScanResultRepository>();
            await results.UpdateStageAsync(item.ScanResultId, "Failed", "TimedOut", "The scan exceeded the configured time limit.", CancellationToken.None);
            await results.SetErrorAsync(item.ScanResultId, "The scan exceeded the configured time limit.", CancellationToken.None);
            var projects = scope.ServiceProvider.GetRequiredService<IScanProjectRepository>();
            var project = await projects.GetByIdAsync(item.ProjectId, CancellationToken.None);
            if (project != null)
            {
                project.Status = "Failed";
                await projects.UpdateAsync(project, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record the timeout for scan {ScanResultId}", item.ScanResultId);
        }
    }
}
