using ProdFalcon.Application.Interfaces;
using ProdFalcon.Domain.Entities;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AuditService(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task LogAsync(
        Guid tenantId,
        int? userId,
        string action,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            tenantId = _tenantProvider.TenantId;

        if (tenantId == Guid.Empty)
            return;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId ?? _tenantProvider.UserId,
            Action = action,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
