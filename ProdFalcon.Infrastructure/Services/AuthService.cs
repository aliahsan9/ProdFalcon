using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProdFalcon.Application.DTOs.Auth;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Exceptions;
using System.Text.RegularExpressions;

namespace ProdFalcon.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;

    public AuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        ITenantProvider tenantProvider,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _tenantProvider = tenantProvider;
        _auditService = auditService;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(x => x.Email.ToLower() == email);
        if (exists)
            throw new ConflictException("User already exists.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var user = new AppUser
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsSuperAdmin = IsConfiguredSuperAdmin(email)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var orgName = string.IsNullOrWhiteSpace(dto.OrganizationName)
            ? $"{user.FullName}'s Workspace"
            : dto.OrganizationName.Trim();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = orgName,
            Slug = await GenerateUniqueSlugAsync(orgName),
            OwnerUserId = user.Id,
            Plan = SubscriptionTier.Free,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);

        _tenantProvider.SetTenant(tenant.Id, user.Id, tenant.Name, tenant.Plan, TenantRole.Owner);

        var member = new TenantMember
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Owner,
            Status = MemberStatus.Active,
            InvitedAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow
        };

        _context.TenantMembers.Add(member);

        var subscription = new UserSubscription
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Tier = SubscriptionTier.Free,
            IsActive = true
        };

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(tenant.Id, user.Id, "UserRegistered", $"{{\"email\":\"{user.Email}\"}}");
        await _auditService.LogAsync(tenant.Id, user.Id, "TenantCreated", $"{{\"slug\":\"{tenant.Slug}\"}}");

        await transaction.CommitAsync();

        var token = _jwtService.GenerateToken(new JwtTenantContext
        {
            User = user,
            Tenant = tenant,
            Role = TenantRole.Owner,
            Plan = tenant.Plan
        });

        return BuildResponse(user, tenant, TenantRole.Owner, tenant.Plan, token);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (IsConfiguredSuperAdmin(email) && !user.IsSuperAdmin)
        {
            user.IsSuperAdmin = true;
            await _context.SaveChangesAsync();
        }

        var membership = await _context.TenantMembers
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id
                        && m.Status == MemberStatus.Active
                        && m.Tenant != null
                        && !m.Tenant.IsDeleted
                        && m.Tenant.Status == TenantStatus.Active)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .FirstOrDefaultAsync();

        if (membership?.Tenant == null)
            throw new UnauthorizedAccessException("No active organization membership found.");

        if (membership.Tenant.Status == TenantStatus.Suspended)
            throw new UnauthorizedAccessException("Organization is suspended.");

        _tenantProvider.SetTenant(
            membership.TenantId,
            user.Id,
            membership.Tenant.Name,
            membership.Tenant.Plan,
            membership.Role);

        var token = _jwtService.GenerateToken(new JwtTenantContext
        {
            User = user,
            Tenant = membership.Tenant,
            Role = membership.Role,
            Plan = membership.Tenant.Plan
        });

        return BuildResponse(user, membership.Tenant, membership.Role, membership.Tenant.Plan, token);
    }

    private bool IsConfiguredSuperAdmin(string email)
    {
        var emails = _configuration.GetSection("SuperAdmin:Emails").Get<string[]>() ?? [];
        return emails.Any(e => string.Equals(e?.Trim(), email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "workspace";

        var slug = baseSlug;
        var suffix = 1;
        while (await _context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static AuthResponseDto BuildResponse(
        AppUser user,
        Tenant tenant,
        TenantRole role,
        SubscriptionTier plan,
        string token) =>
        new()
        {
            Token = token,
            Email = user.Email,
            FullName = user.FullName,
            TenantId = tenant.Id,
            Organization = tenant.Name,
            Role = role.ToString(),
            Plan = plan.ToString(),
            IsSuperAdmin = user.IsSuperAdmin
        };
}
