using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Repositories;

public class ScanResultRepository : IScanResultRepository
{
    private readonly ApplicationDbContext _db;

    public ScanResultRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ScanResult> SaveAsync(ScanResult result, CancellationToken cancellationToken = default)
    {
        _db.ScanResults.Add(result);
        await _db.SaveChangesAsync(cancellationToken);

        return await _db.ScanResults
            .Include(r => r.Issues)
            .FirstAsync(r => r.Id == result.Id, cancellationToken);
    }

    public Task<ScanResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.ScanResults
            .Include(r => r.Issues)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScanResult>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _db.ScanResults
            .Include(r => r.Issues)
            .Where(r => r.ScanProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScanResult>> GetRecentAsync(int take, CancellationToken cancellationToken = default) =>
        await _db.ScanResults
            .Include(r => r.Issues)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
