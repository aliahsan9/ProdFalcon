using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/policies")]
public class PoliciesController : ControllerBase
{
    private readonly ISecurityPolicyService _policies;

    public PoliciesController(ISecurityPolicyService policies)
    {
        _policies = policies;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _policies.GetAsync(cancellationToken);
            return Ok(ApiResponse<SecurityPolicyDto>.Ok(policy));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<SecurityPolicyDto>.Fail(ex.Message));
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SecurityPolicyDto request, CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _policies.UpdateAsync(request, cancellationToken);
            return Ok(ApiResponse<SecurityPolicyDto>.Ok(policy, "Quality gate saved."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<SecurityPolicyDto>.Fail(ex.Message));
        }
    }
}
