using ProdFalcon.Domain.Enums;

namespace ProdFalcon.Application.Security;

public static class TenantAccess
{
    public static bool CanScan(TenantRole? role, bool superAdmin) =>
        AtLeast(role, TenantRole.Developer, superAdmin);

    public static bool CanReview(TenantRole? role, bool superAdmin) =>
        AtLeast(role, TenantRole.SecurityReviewer, superAdmin);

    public static bool CanAdminister(TenantRole? role, bool superAdmin) =>
        AtLeast(role, TenantRole.Admin, superAdmin);

    public static bool AtLeast(TenantRole? role, TenantRole minimum, bool superAdmin = false) =>
        superAdmin || (role.HasValue && role.Value >= minimum);

    public static void Require(bool allowed, string message)
    {
        if (!allowed)
            throw new UnauthorizedAccessException(message);
    }
}
