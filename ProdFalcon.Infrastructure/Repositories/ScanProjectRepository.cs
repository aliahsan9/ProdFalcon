using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Repositories;

public class ScanProjectRepository : IScanProjectRepository
{
    private readonly ApplicationDbContext _db;

    public ScanProjectRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ScanProject> CreateAsync(ScanProject project, CancellationToken cancellationToken = default)
    {
        _db.ScanProjects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return project;
    }

    public Task<ScanProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ScanProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScanProject>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.ScanProjects
            .OrderByDescending(p => p.UploadedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(ScanProject project, CancellationToken cancellationToken = default)
    {
        _db.ScanProjects.Update(project);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
