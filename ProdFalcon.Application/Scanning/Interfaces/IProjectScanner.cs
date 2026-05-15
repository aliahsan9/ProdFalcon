using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces
{
    public interface IProjectScanner
    {
        Task<ScanResult> ScanAsync(string projectPath, CancellationToken cancellationToken = default);
    }
}