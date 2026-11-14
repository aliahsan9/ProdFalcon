using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.DTOs.Auth;
using ProdFalcon.Application.DTOs.Tenants;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Shared.Exceptions;

namespace ProdFalcon.Infrastructure.Services;

public class TenantMemberService : ITenantMemberService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _auditService;

    public TenantMemberService(
        ApplicationDbContext db,
        ITenantProvider tenantProvider,
        IJwtService jwtService,
        IAuditService auditService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _jwtService = jwtService;
        _auditService = auditService;
    }

    public async Task<InviteResultDto> InviteAsync(InviteMemberDto dto, CancellationToken cancellationToken = default)
    {
        EnsureCanManageMembers();

        var email = dto.Email.Trim().ToLowerInvariant();
        var role = dto.Role == TenantRole.Owner ? TenantRole.Admin : dto.Role;

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
        {
            var alreadyMember = await _db.TenantMembers
                .AnyAsync(m => m.UserId == existingUser.Id && m.Status != MemberStatus.Removed, cancellationToken);

            if (alreadyMember)
                throw new ConflictException("User is already a member of this organization.");
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", string.Empty)
            .Replace("/", string.Empty)
            .TrimEnd('=');

        var expires = DateTime.UtcNow.AddDays(7);

        if (existingUser == null)
        {
            existingUser = new AppUser
            {
                FullName = string.IsNullOrWhiteSpace(dto.FullName) ? email : dto.FullName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"))
            };
            _db.Users.Add(existingUser);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var member = new TenantMember
        {
            TenantId = _tenantProvider.TenantId,
            UserId = existingUser.Id,
            Role = role,
            Status = MemberStatus.Invited,
            InvitedAt = DateTime.UtcNow,
            InviteToken = token,
            InviteExpiresAt = expires
        };

        _db.TenantMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _tenantProvider.TenantId,
            _tenantProvider.UserId,
            "MemberInvited",
            $"{{\"email\":\"{email}\",\"role\":\"{role}\"}}",
            cancellationToken);

        return new InviteResultDto
        {
            Email = email,
            InviteToken = token,
            ExpiresAt = expires,
            Role = role.ToString()
        };
    }

    public async Task<AuthResponseDto> AcceptInviteAsync(AcceptInviteDto dto, CancellationToken cancellationToken = default)
    {
        var member = await _db.TenantMembers
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.InviteToken == dto.Token && m.Status == MemberStatus.Invited,
                cancellationToken);

        if (member?.Tenant == null || member.User == null)
            throw new KeyNotFoundException("Invite not found.");

        if (member.InviteExpiresAt.HasValue && member.InviteExpiresAt.Value < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invite has expired.");

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            member.User.FullName = dto.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Password))
            member.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        member.Status = MemberStatus.Active;
        member.JoinedAt = DateTime.UtcNow;
        member.InviteToken = null;
        member.InviteExpiresAt = null;

        _tenantProvider.SetTenant(member.TenantId, member.UserId, member.Tenant.Name, member.Tenant.Plan, member.Role);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            member.TenantId,
            member.UserId,
            "InviteAccepted",
            null,
            cancellationToken);

        var token = _jwtService.GenerateToken(new JwtTenantContext
        {
            User = member.User,
            Tenant = member.Tenant,
            Role = member.Role,
            Plan = member.Tenant.Plan
        });

        return new AuthResponseDto
        {
            Token = token,
            Email = member.User.Email,
            FullName = member.User.FullName,
            TenantId = member.Tenant.Id,
            Organization = member.Tenant.Name,
            Role = member.Role.ToString(),
            Plan = member.Tenant.Plan.ToString(),
            IsSuperAdmin = member.User.IsSuperAdmin
        };
    }

    public async Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.IsAuthenticated || _tenantProvider.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Not authenticated.");

        var members = await _db.TenantMembers
            .Include(m => m.User)
            .Where(m => m.Status != MemberStatus.Removed)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.User!.Email)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return members.Select(m => new TenantMemberDto
        {
            Id = m.Id,
            UserId = m.UserId,
            Email = m.User?.Email ?? string.Empty,
            FullName = m.User?.FullName ?? string.Empty,
            Role = m.Role.ToString(),
            Status = m.Status.ToString(),
            InvitedAt = m.InvitedAt,
            JoinedAt = m.JoinedAt
        }).ToList();
    }

    public async Task<TenantMemberDto> UpdateRoleAsync(int memberId, TenantRole role, CancellationToken cancellationToken = default)
    {
        EnsureCanManageMembers();
        if (role == TenantRole.Owner)
            throw new InvalidOperationException("Ownership stays with the person who created the workspace.");

        var member = await _db.TenantMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && item.Status != MemberStatus.Removed, cancellationToken);

        if (member == null)
            throw new KeyNotFoundException("Member was not found.");

        if (member.Role == TenantRole.Owner)
            throw new InvalidOperationException("The workspace owner role cannot be changed here.");

        if (_tenantProvider.Role == TenantRole.Admin && member.Role == TenantRole.Admin && !_tenantProvider.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only the owner can change another admin.");

        member.Role = role;
        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            _tenantProvider.TenantId,
            _tenantProvider.UserId,
            "MemberRoleChanged",
            $"{{\"memberId\":{member.Id},\"role\":\"{role}\"}}",
            cancellationToken);

        return ToDto(member);
    }

    public async Task RemoveAsync(int memberId, CancellationToken cancellationToken = default)
    {
        EnsureCanManageMembers();
        var member = await _db.TenantMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && item.Status != MemberStatus.Removed, cancellationToken);

        if (member == null)
            throw new KeyNotFoundException("Member was not found.");

        if (member.Role == TenantRole.Owner)
            throw new InvalidOperationException("The workspace owner cannot be removed.");

        if (member.UserId == _tenantProvider.UserId)
            throw new InvalidOperationException("You cannot remove yourself from the workspace.");

        if (_tenantProvider.Role == TenantRole.Admin && member.Role == TenantRole.Admin && !_tenantProvider.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only the owner can remove another admin.");

        member.Status = MemberStatus.Removed;
        member.InviteToken = null;
        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            _tenantProvider.TenantId,
            _tenantProvider.UserId,
            "MemberRemoved",
            $"{{\"memberId\":{member.Id}}}",
            cancellationToken);
    }

    private static TenantMemberDto ToDto(TenantMember member) => new()
    {
        Id = member.Id,
        UserId = member.UserId,
        Email = member.User?.Email ?? string.Empty,
        FullName = member.User?.FullName ?? string.Empty,
        Role = member.Role.ToString(),
        Status = member.Status.ToString(),
        InvitedAt = member.InvitedAt,
        JoinedAt = member.JoinedAt
    };

    private void EnsureCanManageMembers()
    {
        if (_tenantProvider.TenantId == Guid.Empty || _tenantProvider.UserId is null)
            throw new UnauthorizedAccessException("Not authenticated.");

        if (_tenantProvider.Role is not (TenantRole.Owner or TenantRole.Admin) && !_tenantProvider.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only Owners and Admins can invite members.");
    }
}
