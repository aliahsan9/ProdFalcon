namespace ProdFalcon.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    public string Organization { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Plan { get; set; } = string.Empty;

    public bool IsSuperAdmin { get; set; }
}
