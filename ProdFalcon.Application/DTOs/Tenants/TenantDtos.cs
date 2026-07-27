using ProdFalcon.Domain.Enums;

namespace ProdFalcon.Application.DTOs.Tenants;

public class InviteMemberDto
{
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public TenantRole Role { get; set; } = TenantRole.Developer;
}

public class AcceptInviteDto
{
    public string Token { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? FullName { get; set; }
}

public class TenantMemberDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime InvitedAt { get; set; }

    public DateTime? JoinedAt { get; set; }
}

public class InviteResultDto
{
    public string Email { get; set; } = string.Empty;

    public string InviteToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public string Role { get; set; } = string.Empty;
}
