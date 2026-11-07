using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class ProjectWorkspaceStore : IProjectWorkspaceStore
{
    private readonly ApplicationDbContext _db;

    public ProjectWorkspaceStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<TrackedFinding>> GetTrackedAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _db.TrackedFindings.Where(finding => finding.ScanProjectId == projectId).ToListAsync(cancellationToken);

    public async Task SaveTrackedAsync(IReadOnlyList<TrackedFinding> findings, CancellationToken cancellationToken = default)
    {
        foreach (var finding in findings)
        {
            if (finding.Id == 0)
                _db.TrackedFindings.Add(finding);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCopilotMessageAsync(CopilotMessage message, CancellationToken cancellationToken = default)
    {
        _db.CopilotMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CopilotMessage>> GetCopilotMessagesAsync(int scanResultId, CancellationToken cancellationToken = default) =>
        await _db.CopilotMessages
            .AsNoTracking()
            .Where(message => message.ScanResultId == scanResultId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<RepositoryRegistration> AddRegistrationAsync(RepositoryRegistration registration, CancellationToken cancellationToken = default)
    {
        _db.RepositoryRegistrations.Add(registration);
        await _db.SaveChangesAsync(cancellationToken);
        return registration;
    }

    public async Task<IReadOnlyList<RepositoryRegistration>> ListRegistrationsAsync(CancellationToken cancellationToken = default) =>
        await _db.RepositoryRegistrations.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);

    public Task<RepositoryRegistration?> FindRegistrationByRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default) =>
        _db.RepositoryRegistrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Owner == owner && item.Name == name, cancellationToken);

    public Task<RepositoryRegistration?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _db.RepositoryRegistrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
}
