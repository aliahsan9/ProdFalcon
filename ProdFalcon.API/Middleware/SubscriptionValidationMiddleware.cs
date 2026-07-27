using System.Security.Claims;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.API.Middleware;

public class SubscriptionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IStripeSubscriptionService subscriptionService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/ai", StringComparison.OrdinalIgnoreCase))
        {
            var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
            if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
            {
                var tier = subscriptionService.GetTierForCurrentTenant();

                if (tier == SubscriptionTier.Free)
                {
                    context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = "AI suggestions require a Pro or Enterprise subscription."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
