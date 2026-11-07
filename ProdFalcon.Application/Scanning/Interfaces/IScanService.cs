using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IScanService
{
    Task<ScanResultDto> ScanProjectAsync(
        Guid projectId,
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<ScanResultDto> ExecuteQueuedScanAsync(
        Guid projectId,
        int scanResultId,
        string? projectPath,
        string? accessToken,
        CancellationToken cancellationToken = default);
}
