using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Security;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class CollaborationService : ICollaborationService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public CollaborationService(ApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CollaborationNoteDto>> ListAsync(int? scanResultId, CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Not authenticated.");

        var query = _db.CollaborationNotes.AsNoTracking();
        if (scanResultId.HasValue)
            query = query.Where(item => item.ScanResultId == scanResultId.Value);

        var notes = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return notes.Select(ToDto).ToList();
    }

    public async Task<CollaborationNoteDto> AddAsync(int? scanResultId, Guid? projectId, string body, CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty || _tenant.UserId is null)
            throw new UnauthorizedAccessException("Not authenticated.");

        TenantAccess.Require(
            TenantAccess.CanScan(_tenant.Role, _tenant.IsSuperAdmin),
            "Viewers can read notes. Ask an admin for a developer role to comment.");

        var text = body.Trim();
        if (text.Length < 2)
            throw new InvalidOperationException("Write a short note before saving.");
        if (text.Length > 2000)
            text = text[..2000];

        var author = await _db.Users.AsNoTracking()
            .Where(user => user.Id == _tenant.UserId.Value)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var note = new CollaborationNote
        {
            TenantId = _tenant.TenantId,
            ScanResultId = scanResultId,
            ProjectId = projectId,
            AuthorUserId = _tenant.UserId.Value,
            AuthorName = string.IsNullOrWhiteSpace(author) ? "Teammate" : author.Trim(),
            Body = text,
            CreatedAt = DateTime.UtcNow
        };
        _db.CollaborationNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    private static CollaborationNoteDto ToDto(CollaborationNote note) => new()
    {
        Id = note.Id,
        ScanResultId = note.ScanResultId,
        ProjectId = note.ProjectId,
        AuthorName = note.AuthorName,
        Body = note.Body,
        CreatedAt = note.CreatedAt
    };
}
