namespace ProdFalcon.Application.DTOs.Auth;

public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Optional organization name. Defaults to "{FullName}'s Workspace".</summary>
    public string? OrganizationName { get; set; }
}