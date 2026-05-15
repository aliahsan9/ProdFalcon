using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IScanService
{
    Task<ScanResultDto> ScanProjectAsync(
        Guid projectId,
        string projectPath,
        CancellationToken cancellationToken = default);
}
