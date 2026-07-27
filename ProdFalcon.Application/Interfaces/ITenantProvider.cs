using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Application.Interfaces;

public interface ITenantProvider
{
    Guid TenantId { get; }

    int? UserId { get; }

    string? Organization { get; }

    SubscriptionTier Plan { get; }

    TenantRole? Role { get; }

    bool IsAuthenticated { get; }

    bool IsSuperAdmin { get; }

    /// <summary>
    /// Used during registration / background jobs when no JWT is present.
    /// </summary>
    void SetTenant(Guid tenantId, int? userId = null, string? organization = null, SubscriptionTier? plan = null, TenantRole? role = null);
}
