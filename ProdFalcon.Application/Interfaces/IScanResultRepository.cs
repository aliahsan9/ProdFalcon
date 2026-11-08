using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IScanResultRepository
{
    Task<ScanResult> SaveAsync(ScanResult result, CancellationToken cancellationToken = default);
    Task<ScanResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanResult>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanResult>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<ScanResult> UpdateGraphAsync(ScanResult result, CancellationToken cancellationToken = default);
    Task UpdateStageAsync(int id, string status, string stage, string message, CancellationToken cancellationToken = default);
    Task SetErrorAsync(int id, string message, CancellationToken cancellationToken = default);
    Task<PreviousScanHeader?> GetLatestCompletedAsync(Guid projectId, int excludeScanResultId, CancellationToken cancellationToken = default);
}

public sealed record PreviousScanHeader(int Id, int Score, string? CommitSha, string? Branch);
