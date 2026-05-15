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

        if (path.StartsWith("/api/ai", StringComparison.OrdinalIgnoreCase)
            && !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var userId = ResolveUserId(context);
            var tier = subscriptionService.GetTierForUser(userId);

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

        await _next(context);
    }

    private static int ResolveUserId(HttpContext context)
    {
        var claim = context.User?.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "id");
        return claim != null && int.TryParse(claim.Value, out var userId) ? userId : 0;
    }
}
