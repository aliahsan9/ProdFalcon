using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet]
    public async Task<IActionResult> Overview([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var overview = await _analytics.GetOverviewAsync(days, cancellationToken);
        return Ok(ApiResponse<AnalyticsOverviewDto>.Ok(overview));
    }
}
