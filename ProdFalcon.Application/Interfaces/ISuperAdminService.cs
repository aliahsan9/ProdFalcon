using ProdFalcon.Application.DTOs.Admin;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Application.Interfaces;

public interface ISuperAdminService
{
    Task<IReadOnlyList<TenantAdminDto>> ListTenantsAsync(CancellationToken cancellationToken = default);

    Task SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task UpdateTenantPlanAsync(Guid tenantId, SubscriptionTier plan, CancellationToken cancellationToken = default);
}
