using ProdFalcon.Domain.Enums;
using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Domain.Entities;

public class TenantMember : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public int UserId { get; set; }

    public AppUser? User { get; set; }

    public TenantRole Role { get; set; } = TenantRole.Viewer;

    public MemberStatus Status { get; set; } = MemberStatus.Active;

    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

    public DateTime? JoinedAt { get; set; }

    public string? InviteToken { get; set; }

    public DateTime? InviteExpiresAt { get; set; }
}
