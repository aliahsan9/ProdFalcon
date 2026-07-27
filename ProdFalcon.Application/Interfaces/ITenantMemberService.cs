using ProdFalcon.Application.DTOs.Auth;
using ProdFalcon.Application.DTOs.Tenants;

namespace ProdFalcon.Application.Interfaces;

public interface ITenantMemberService
{
    Task<InviteResultDto> InviteAsync(InviteMemberDto dto, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> AcceptInviteAsync(AcceptInviteDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(CancellationToken cancellationToken = default);
}
