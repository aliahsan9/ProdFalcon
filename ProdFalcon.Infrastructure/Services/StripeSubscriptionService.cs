using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Infrastructure.Services;

public interface IStripeSubscriptionService
{
    Task<string> CreateCheckoutSessionAsync(int userId, SubscriptionTier tier, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);

    SubscriptionTier GetTierForUser(int userId);

    SubscriptionTier GetTierForCurrentTenant();
}

public class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeSubscriptionService> _logger;
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public StripeSubscriptionService(
        IConfiguration configuration,
        ILogger<StripeSubscriptionService> logger,
        ApplicationDbContext db,
        ITenantProvider tenantProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public Task<string> CreateCheckoutSessionAsync(int userId, SubscriptionTier tier, CancellationToken cancellationToken = default)
    {
        var stubUrl = _configuration["Stripe:CheckoutSuccessUrl"] ?? "https://localhost/checkout/success";
        _logger.LogInformation(
            "Stripe checkout stub for user {UserId}, tenant {TenantId}, tier {Tier}",
            userId,
            _tenantProvider.TenantId,
            tier);
        return Task.FromResult($"{stubUrl}?tenantId={_tenantProvider.TenantId}&tier={(int)tier}");
    }

    public Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stripe webhook received (stub). Signature present: {HasSignature}", !string.IsNullOrWhiteSpace(signature));
        return Task.CompletedTask;
    }

    public SubscriptionTier GetTierForUser(int userId)
    {
        if (_tenantProvider.TenantId != Guid.Empty)
            return GetTierForCurrentTenant();

        var sub = _db.Subscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return sub?.Tier ?? SubscriptionTier.Free;
    }

    public SubscriptionTier GetTierForCurrentTenant()
    {
        if (_tenantProvider.Plan != SubscriptionTier.Free)
            return _tenantProvider.Plan;

        if (_tenantProvider.TenantId == Guid.Empty)
            return SubscriptionTier.Free;

        var sub = _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return sub?.Tier ?? _tenantProvider.Plan;
    }
}
