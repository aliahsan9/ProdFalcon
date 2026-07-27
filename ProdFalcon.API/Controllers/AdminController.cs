using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.DTOs.Admin;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Shared.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ISuperAdminService _superAdminService;
    private readonly ITenantProvider _tenantProvider;

    public AdminController(ISuperAdminService superAdminService, ITenantProvider tenantProvider)
    {
        _superAdminService = superAdminService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("SuperAdmin access required."));

        var tenants = await _superAdminService.ListTenantsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantAdminDto>>.Ok(tenants));
    }

    [HttpPost("tenants/{tenantId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("SuperAdmin access required."));

        await _superAdminService.SuspendTenantAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tenantId }, "Tenant suspended."));
    }

    [HttpDelete("tenants/{tenantId:guid}")]
    public async Task<IActionResult> Delete(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("SuperAdmin access required."));

        await _superAdminService.DeleteTenantAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tenantId }, "Tenant deleted."));
    }

    [HttpPut("tenants/{tenantId:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(Guid tenantId, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.IsSuperAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("SuperAdmin access required."));

        await _superAdminService.UpdateTenantPlanAsync(tenantId, request.Plan, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tenantId, plan = request.Plan.ToString() }, "Plan updated."));
    }
}

public class UpdatePlanRequest
{
    public SubscriptionTier Plan { get; set; } = SubscriptionTier.Pro;
}
