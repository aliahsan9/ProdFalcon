using System.Security.Claims;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Tenancy;

namespace ProdFalcon.API.Authorization;

public static class TenantPolicies
{
    public const string CanScan = "CanScan";
    public const string CanReview = "CanReview";
    public const string CanAdminister = "CanAdminister";

    public static bool AtLeast(ClaimsPrincipal user, TenantRole minimum)
    {
        if (string.Equals(user.FindFirstValue(TenantClaimTypes.IsSuperAdmin), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        var value = user.FindFirstValue(TenantClaimTypes.Role) ?? user.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<TenantRole>(value, ignoreCase: true, out var role) && role >= minimum;
    }
}
