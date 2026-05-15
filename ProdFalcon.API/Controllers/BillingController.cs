using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IStripeSubscriptionService _stripeService;

    public BillingController(IStripeSubscriptionService stripeService)
    {
        _stripeService = stripeService;
    }

    [HttpGet("subscription")]
    public IActionResult GetSubscription([FromQuery] int userId = 0)
    {
        var tier = _stripeService.GetTierForUser(userId);
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
            limits,
            usage = new { scansUsed = 0, scansRemaining = limits.scansPerMonth }
        }));
    }
}
