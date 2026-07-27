using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeController : ControllerBase
{
    private readonly IStripeSubscriptionService _stripeService;
    private readonly ITenantProvider _tenantProvider;

    public StripeController(IStripeSubscriptionService stripeService, ITenantProvider tenantProvider)
    {
        _stripeService = stripeService;
        _tenantProvider = tenantProvider;
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var userId = _tenantProvider.UserId
            ?? throw new UnauthorizedAccessException("Not authenticated.");

        var url = await _stripeService.CreateCheckoutSessionAsync(userId, request.Tier, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { checkoutUrl = url }));
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        await _stripeService.HandleWebhookAsync(payload, signature, cancellationToken);
        return Ok();
    }
}

public class CheckoutRequest
{
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Pro;
}
