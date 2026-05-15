using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces
{
    public interface IScanService
    {
        Task<ScanResultDto> ScanProjectAsync(string projectPath, CancellationToken cancellationToken = default);
    }
}