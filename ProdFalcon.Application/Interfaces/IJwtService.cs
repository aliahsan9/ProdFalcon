using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Application.Interfaces;

public sealed class JwtTenantContext
{
    public required AppUser User { get; init; }
    public required Tenant Tenant { get; init; }
    public required TenantRole Role { get; init; }
    public SubscriptionTier Plan { get; init; } = SubscriptionTier.Free;
}

public interface IJwtService
{
    string GenerateToken(JwtTenantContext context);
}
