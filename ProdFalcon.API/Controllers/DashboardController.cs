using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<DashboardSummaryDto>.Ok(summary));
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await _dashboardService.GetProjectsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProjectDashboardDto>>.Ok(projects));
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(CancellationToken cancellationToken)
    {
        var trends = await _dashboardService.GetTrendsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanTrendDto>>.Ok(trends));
    }
}
