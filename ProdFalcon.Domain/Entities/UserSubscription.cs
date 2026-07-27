using ProdFalcon.Domain.Interfaces;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Domain.Entities;

public class UserSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public int UserId { get; set; }

    public AppUser? User { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public string StripeCustomerId { get; set; } = string.Empty;

    public string StripeSubscriptionId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
