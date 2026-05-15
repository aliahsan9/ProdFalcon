using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public ProjectsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await _dashboardService.GetProjectsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProjectDashboardDto>>.Ok(projects));
    }
}
