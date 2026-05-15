using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeController : ControllerBase
{
    private readonly IStripeSubscriptionService _stripeService;

    public StripeController(IStripeSubscriptionService stripeService)
    {
        _stripeService = stripeService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var url = await _stripeService.CreateCheckoutSessionAsync(request.UserId, request.Tier, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { checkoutUrl = url }));
    }

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
    public int UserId { get; set; }
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Pro;
}
