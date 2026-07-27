using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.DTOs.Admin;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Infrastructure.Services;

public class SuperAdminService : ISuperAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditService _auditService;

    public SuperAdminService(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        IAuditService auditService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<TenantAdminDto>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var members = await _db.TenantMembers
            .IgnoreQueryFilters()
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var projects = await _db.ScanProjects
            .IgnoreQueryFilters()
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var scans = await _db.ScanResults
            .IgnoreQueryFilters()
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var memberMap = members.ToDictionary(x => x.TenantId, x => x.Count);
        var projectMap = projects.ToDictionary(x => x.TenantId, x => x.Count);
        var scanMap = scans.ToDictionary(x => x.TenantId, x => x.Count);

        return tenants.Select(t => new TenantAdminDto
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Status = t.Status.ToString(),
            Plan = t.Plan.ToString(),
            StorageUsed = t.StorageUsed,
            StorageLimit = t.StorageLimit,
            ScanLimit = t.ScanLimit,
            AIUsage = t.AIUsage,
            OwnerUserId = t.OwnerUserId,
            CreatedAt = t.CreatedAt,
            MemberCount = memberMap.GetValueOrDefault(t.Id),
            ProjectCount = projectMap.GetValueOrDefault(t.Id),
            ScanCount = scanMap.GetValueOrDefault(t.Id)
        }).ToList();
    }

    public async Task SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Tenant not found.");

        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(tenantId, _tenantProvider.UserId, "TenantSuspended", null, cancellationToken);
    }

    public async Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Tenant not found.");

        tenant.IsDeleted = true;
        tenant.Status = TenantStatus.Deleted;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(tenantId, _tenantProvider.UserId, "TenantDeleted", null, cancellationToken);
    }

    public async Task UpdateTenantPlanAsync(Guid tenantId, SubscriptionTier plan, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Tenant not found.");

        tenant.Plan = plan;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.ScanLimit = plan switch
        {
            SubscriptionTier.Enterprise => -1,
            SubscriptionTier.Pro => 100,
            _ => 5
        };
        tenant.StorageLimit = plan switch
        {
            SubscriptionTier.Enterprise => 107_374_182_400,
            SubscriptionTier.Pro => 10_737_418_240,
            _ => 1_073_741_824
        };

        var subscriptions = await _db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        foreach (var sub in subscriptions)
        {
            sub.Tier = plan;
            sub.IsActive = plan != SubscriptionTier.Free;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(tenantId, _tenantProvider.UserId, "TenantPlanUpdated", $"{{\"plan\":\"{plan}\"}}", cancellationToken);
    }

    private void EnsureSuperAdmin()
    {
        if (!_tenantProvider.IsSuperAdmin)
            throw new UnauthorizedAccessException("SuperAdmin access required.");
    }
}
