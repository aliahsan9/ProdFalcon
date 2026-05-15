using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IScanProjectRepository
{
    Task<ScanProject> CreateAsync(ScanProject project, CancellationToken cancellationToken = default);
    Task<ScanProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ScanProject project, CancellationToken cancellationToken = default);
}
