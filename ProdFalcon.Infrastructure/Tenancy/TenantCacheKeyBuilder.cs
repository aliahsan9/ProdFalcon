using ProdFalcon.Application.Interfaces;

namespace ProdFalcon.Infrastructure.Tenancy;

public class TenantCacheKeyBuilder : ITenantCacheKeyBuilder
{
    private readonly ITenantProvider _tenantProvider;

    public TenantCacheKeyBuilder(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public string Build(string key)
    {
        var tenantId = _tenantProvider.TenantId;
        return tenantId == Guid.Empty
            ? $"tenant:none:{key}"
            : $"tenant:{tenantId}:{key}";
    }
}

/// <summary>
/// Helper for future SignalR hubs — broadcast only to the authenticated tenant group.
/// </summary>
public static class TenantHubGroups
{
    public static string ForTenant(Guid tenantId) => $"tenant-{tenantId:N}";
}
