using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IScanResultRepository
{
    Task<ScanResult> SaveAsync(ScanResult result, CancellationToken cancellationToken = default);
    Task<ScanResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanResult>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanResult>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
