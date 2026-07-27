using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int? OwnerUserId { get; set; }

    public AppUser? OwnerUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public SubscriptionTier Plan { get; set; } = SubscriptionTier.Free;

    public long StorageUsed { get; set; }

    public long StorageLimit { get; set; } = 1_073_741_824; // 1 GB default

    public int ScanLimit { get; set; } = 5;

    public int AIUsage { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
}
