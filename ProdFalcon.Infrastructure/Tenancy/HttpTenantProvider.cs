using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Infrastructure.Tenancy;

public static class TenantClaimTypes
{
    public const string TenantId = "tenantId";
    public const string Organization = "organization";
    public const string Plan = "plan";
    public const string Role = "role";
    public const string IsSuperAdmin = "is_super_admin";
}

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _overrideTenantId;
    private int? _overrideUserId;
    private string? _overrideOrganization;
    private SubscriptionTier? _overridePlan;
    private TenantRole? _overrideRole;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            if (_overrideTenantId.HasValue)
                return _overrideTenantId.Value;

            var value = User?.FindFirstValue(TenantClaimTypes.TenantId)
                ?? User?.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public int? UserId
    {
        get
        {
            if (_overrideUserId.HasValue)
                return _overrideUserId;

            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Organization =>
        _overrideOrganization ?? User?.FindFirstValue(TenantClaimTypes.Organization);

    public SubscriptionTier Plan
    {
        get
        {
            if (_overridePlan.HasValue)
                return _overridePlan.Value;

            var value = User?.FindFirstValue(TenantClaimTypes.Plan);
            return Enum.TryParse<SubscriptionTier>(value, ignoreCase: true, out var plan)
                ? plan
                : SubscriptionTier.Free;
        }
    }

    public TenantRole? Role
    {
        get
        {
            if (_overrideRole.HasValue)
                return _overrideRole;

            var value = User?.FindFirstValue(TenantClaimTypes.Role)
                ?? User?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<TenantRole>(value, ignoreCase: true, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true || _overrideTenantId.HasValue;

    public bool IsSuperAdmin
    {
        get
        {
            var value = User?.FindFirstValue(TenantClaimTypes.IsSuperAdmin);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetTenant(
        Guid tenantId,
        int? userId = null,
        string? organization = null,
        SubscriptionTier? plan = null,
        TenantRole? role = null)
    {
        _overrideTenantId = tenantId;
        _overrideUserId = userId;
        _overrideOrganization = organization;
        _overridePlan = plan;
        _overrideRole = role;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
}

public class NullTenantProvider : ITenantProvider
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public int? UserId { get; private set; }
    public string? Organization { get; private set; }
    public SubscriptionTier Plan { get; private set; } = SubscriptionTier.Free;
    public TenantRole? Role { get; private set; }
    public bool IsAuthenticated => TenantId != Guid.Empty;
    public bool IsSuperAdmin => false;

    public void SetTenant(
        Guid tenantId,
        int? userId = null,
        string? organization = null,
        SubscriptionTier? plan = null,
        TenantRole? role = null)
    {
        TenantId = tenantId;
        UserId = userId;
        Organization = organization;
        if (plan.HasValue) Plan = plan.Value;
        Role = role;
    }
}
