using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ExamDynamicsAPI.Applications.Services.Auth;

public sealed class ExternalAuthCompletionService : IExternalAuthCompletionService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ExternalAuthCompletionService> _logger;

    public ExternalAuthCompletionService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<ExternalAuthCompletionService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<(bool Success, string Token, string? ErrorMessage)> CompleteExternalLoginAsync(
        ExternalLoginInfo info,
        CancellationToken cancellationToken = default)
    {
        var principal = info.Principal;
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("External login missing email for provider {Provider}.", info.LoginProvider);
            return (false, string.Empty, "Email not provided by the provider.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            var name = principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue("name")
                ?? email.Split('@')[0];

            user = new ApplicationUser
            {
                UserName = await EnsureUniqueUserNameAsync(email),
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                CreatedAt = DateTime.UtcNow
            };

            var randomPassword = GenerateSecureRandomPassword();
            var createResult = await _userManager.CreateAsync(user, randomPassword);
            if (!createResult.Succeeded)
            {
                var err = string.Join(" ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("External user create failed: {Errors}", err);
                return (false, string.Empty, "Could not create account.");
            }

            await _userManager.AddToRoleAsync(user, "Student");
        }

        var existingLogins = await _userManager.GetLoginsAsync(user);
        if (existingLogins.All(l => l.LoginProvider != info.LoginProvider || l.ProviderKey != info.ProviderKey))
        {
            try
            {
                await _userManager.AddLoginAsync(user, info);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AddLoginAsync failed for {Email}", email);
            }
        }

        var jwt = await _tokenService.GenerateJwtTokenAsync(user);
        return (true, jwt, null);
    }

    private async Task<string> EnsureUniqueUserNameAsync(string email)
    {
        var baseName = email.Split('@')[0];
        var name = baseName;
        var i = 0;
        while (await _userManager.FindByNameAsync(name) != null)
        {
            i++;
            name = $"{baseName}{i}";
        }

        return name;
    }

    private static string GenerateSecureRandomPassword()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes) + "Aa1!";
    }
}
