using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/dashboard/summary");
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<DashboardSummaryDto>.Ok(summary));
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/dashboard/projects");
        var projects = await _dashboardService.GetProjectsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProjectDashboardDto>>.Ok(projects));
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/dashboard/trends");
        var trends = await _dashboardService.GetTrendsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanTrendDto>>.Ok(trends));
    }
}
