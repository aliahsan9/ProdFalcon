using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.DTOs.Tenants;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantMemberService _memberService;

    public TenantsController(ITenantMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken)
    {
        var members = await _memberService.GetMembersAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantMemberDto>>.Ok(members));
    }

    [HttpPost("members/invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMemberDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _memberService.InviteAsync(dto, cancellationToken);
            return Ok(ApiResponse<InviteResultDto>.Ok(result, "Invitation created."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<InviteResultDto>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpPost("invites/accept")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _memberService.AcceptInviteAsync(dto, cancellationToken);
            return Ok(ApiResponse<object>.Ok(result, "Invite accepted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
