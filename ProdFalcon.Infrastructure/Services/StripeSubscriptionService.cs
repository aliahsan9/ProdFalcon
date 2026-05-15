using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Infrastructure.Services;

public interface IStripeSubscriptionService
{
    Task<string> CreateCheckoutSessionAsync(int userId, SubscriptionTier tier, CancellationToken cancellationToken = default);
    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);
    SubscriptionTier GetTierForUser(int userId);
}

public class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeSubscriptionService> _logger;

    public StripeSubscriptionService(IConfiguration configuration, ILogger<StripeSubscriptionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> CreateCheckoutSessionAsync(int userId, SubscriptionTier tier, CancellationToken cancellationToken = default)
    {
        var stubUrl = _configuration["Stripe:CheckoutSuccessUrl"] ?? "https://localhost/checkout/success";
        _logger.LogInformation("Stripe checkout stub for user {UserId}, tier {Tier}", userId, tier);
        return Task.FromResult($"{stubUrl}?userId={userId}&tier={(int)tier}");
    }

    public Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stripe webhook received (stub). Signature present: {HasSignature}", !string.IsNullOrWhiteSpace(signature));
        return Task.CompletedTask;
    }

    public SubscriptionTier GetTierForUser(int userId)
    {
        _logger.LogDebug("Returning Free tier for user {UserId} (stub)", userId);
        return SubscriptionTier.Free;
    }
}
