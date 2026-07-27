using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IStripeSubscriptionService _stripeService;
    private readonly ITenantProvider _tenantProvider;

    public BillingController(IStripeSubscriptionService stripeService, ITenantProvider tenantProvider)
    {
        _stripeService = stripeService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("subscription")]
    public IActionResult GetSubscription()
    {
        var tier = _stripeService.GetTierForCurrentTenant();
        var limits = tier switch
        {
            SubscriptionTier.Enterprise => new { scansPerMonth = -1, aiEnabled = true, ciCdEnabled = true },
            SubscriptionTier.Pro => new { scansPerMonth = 100, aiEnabled = true, ciCdEnabled = true },
            _ => new { scansPerMonth = 5, aiEnabled = false, ciCdEnabled = false }
        };

        return Ok(ApiResponse<object>.Ok(new
        {
            tier = tier.ToString(),
            isActive = tier != SubscriptionTier.Free,
            organization = _tenantProvider.Organization,
            tenantId = _tenantProvider.TenantId,
            limits,
            usage = new { scansUsed = 0, scansRemaining = limits.scansPerMonth }
        }));
    }
}
