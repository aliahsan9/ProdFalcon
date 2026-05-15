using ExamDynamicsAPI.Core.DTOs.AuthDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace ExamDynamicsAPI.WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IActivityLogService _activityLog;
    private readonly IAuthPasswordService _authPassword;
    private readonly IExternalAuthCompletionService _externalAuth;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IActivityLogService activityLog,
        IAuthPasswordService authPassword,
        IExternalAuthCompletionService externalAuth,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _activityLog = activityLog;
        _authPassword = authPassword;
        _externalAuth = externalAuth;
        _configuration = configuration;
        _env = env;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponseDto>> Me()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new AuthUserResponseDto
        {
            Id = user.Id,
            Username = user.UserName,
            Email = user.Email,
            Roles = roles
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginRegisterResponseDto>> Login([FromBody] LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var token = await _tokenService.GenerateJwtTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        try
        {
            await _activityLog.LogAsync(user.Id, "Login", "Signed in successfully.");
        }
        catch
        {
            // Activity logging must not block authentication
        }

        return Ok(new LoginRegisterResponseDto
        {
            Token = token,
            User = new AuthUserResponseDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = roles
            }
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginRegisterResponseDto>> Register([FromBody] RegisterDto model)
    {
        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToList() });

        await _userManager.AddToRoleAsync(user, model.Role);

        var created = await _userManager.FindByEmailAsync(model.Email);
        if (created == null)
            return BadRequest(new { message = "Registration failed after create." });

        var token = await _tokenService.GenerateJwtTokenAsync(created);
        var roles = await _userManager.GetRolesAsync(created);

        try
        {
            await _activityLog.LogAsync(created.Id, "AccountCreated", "Welcome to ExamDynamics — your account is ready.");
        }
        catch
        {
        }

        return Ok(new LoginRegisterResponseDto
        {
            Message = "User registered successfully.",
            Token = token,
            User = new AuthUserResponseDto
            {
                Id = created.Id,
                Username = created.UserName,
                Email = created.Email,
                Roles = roles
            }
        });
    }

    /// <summary>
    /// Request a password reset link (email enumeration resistant).
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto model)
    {
        await _authPassword.RequestPasswordResetAsync(model.Email, HttpContext.RequestAborted);
        return Ok(new { message = "If an account exists for that email, you will receive reset instructions shortly." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto model)
    {
        var (success, error) = await _authPassword.ResetPasswordAsync(
            model.Email,
            model.Token,
            model.NewPassword,
            HttpContext.RequestAborted);

        if (!success)
            return BadRequest(new { message = error ?? "Could not reset password." });

        return Ok(new { message = "Your password has been reset. You can sign in with your new password." });
    }

    [HttpGet("oauth/google")]
    public async Task<IActionResult> GoogleLogin()
    {
        var googleId = _configuration["Authentication:Google:ClientId"];
        var googleSecret = _configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback));
            if (string.IsNullOrEmpty(redirectUrl))
                return BadRequest(new { message = "OAuth callback URL could not be resolved." });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        if (IsDemoSocialLoginEnabled())
        {
            return await CompleteDemoSocialLoginAsync(
                GoogleDefaults.AuthenticationScheme,
                "examdynamics-dev-google",
                "demo.google@examdynamics.local",
                "Demo Google User");
        }

        return RedirectToLoginWithOAuthMessage(
            "Google sign-in is not configured. Add Authentication:Google:ClientId and ClientSecret to appsettings or user secrets, " +
            "or set Authentication:DemoSocialLogin:Enabled to true in appsettings.Development.json for local testing.");
    }

    [HttpGet("oauth/facebook")]
    public async Task<IActionResult> FacebookLogin()
    {
        var fbId = _configuration["Authentication:Facebook:AppId"];
        var fbSecret = _configuration["Authentication:Facebook:AppSecret"];
        if (!string.IsNullOrWhiteSpace(fbId) && !string.IsNullOrWhiteSpace(fbSecret))
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback));
            if (string.IsNullOrEmpty(redirectUrl))
                return BadRequest(new { message = "OAuth callback URL could not be resolved." });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Facebook", redirectUrl);
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }

        if (IsDemoSocialLoginEnabled())
        {
            return await CompleteDemoSocialLoginAsync(
                FacebookDefaults.AuthenticationScheme,
                "examdynamics-dev-facebook",
                "demo.facebook@examdynamics.local",
                "Demo Facebook User");
        }

        return RedirectToLoginWithOAuthMessage(
            "Facebook sign-in is not configured. Add Authentication:Facebook:AppId and AppSecret, " +
            "or enable Authentication:DemoSocialLogin in appsettings.Development.json for local testing.");
    }

    /// <summary>
    /// Local dev only: simulates external login without Google/Facebook app credentials.
    /// </summary>
    private bool IsDemoSocialLoginEnabled() =>
        _env.IsDevelopment()
        && _configuration.GetValue("Authentication:DemoSocialLogin:Enabled", false);

    private IActionResult RedirectToLoginWithOAuthMessage(string message)
    {
        var frontend = (_configuration["Frontend:Url"] ?? "http://localhost:4200").TrimEnd('/');
        return Redirect($"{frontend}/login?oauthError={Uri.EscapeDataString(message)}");
    }

    private async Task<IActionResult> CompleteDemoSocialLoginAsync(
        string loginProvider,
        string providerKey,
        string email,
        string displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.NameIdentifier, providerKey)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, loginProvider));
        var info = new ExternalLoginInfo(principal, loginProvider, providerKey, displayName);

        var (success, token, error) = await _externalAuth.CompleteExternalLoginAsync(info, HttpContext.RequestAborted);
        var frontend = (_configuration["Frontend:Url"] ?? "http://localhost:4200").TrimEnd('/');

        if (!success || string.IsNullOrEmpty(token))
            return Redirect($"{frontend}/login?oauthError={Uri.EscapeDataString(error ?? "Demo sign-in failed.")}");

        return Redirect($"{frontend}/auth/callback?token={Uri.EscapeDataString(token)}");
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> ExternalLoginCallback()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        var frontend = (_configuration["Frontend:Url"] ?? "http://localhost:4200").TrimEnd('/');

        if (info == null)
            return Redirect($"{frontend}/login?oauthError={Uri.EscapeDataString("External login failed or was cancelled.")}");

        var (success, token, error) = await _externalAuth.CompleteExternalLoginAsync(info, HttpContext.RequestAborted);

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (!success || string.IsNullOrEmpty(token))
            return Redirect($"{frontend}/login?oauthError={Uri.EscapeDataString(error ?? "Could not complete sign-in.")}");

        return Redirect($"{frontend}/auth/callback?token={Uri.EscapeDataString(token)}");
    }
}
