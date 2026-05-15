using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubController : ControllerBase
{
    private readonly IGitHubIntegrationService _gitHubService;
    private readonly ILogger<GitHubController> _logger;

    public GitHubController(IGitHubIntegrationService gitHubService, ILogger<GitHubController> logger)
    {
        _gitHubService = gitHubService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] GitHubWebhookPayload payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GitHub webhook received: {Action} on {Repo}", payload.Action, payload.Repository);

        if (payload.PullRequestNumber > 0 && !string.IsNullOrWhiteSpace(payload.Repository))
        {
            try
            {
                var result = await _gitHubService.AnalyzePullRequestAsync(
                    payload.Repository,
                    payload.PullRequestNumber,
                    cancellationToken);

                return Ok(ApiResponse<object>.Ok(new
                {
                    message = "PR scan completed",
                    result
                }));
            }
            catch (NotImplementedException ex)
            {
                return Ok(ApiResponse<object>.Ok(new { message = ex.Message, status = "stub" }));
            }
        }

        return Ok(ApiResponse<object>.Ok(new { message = "Webhook acknowledged" }));
    }

    [HttpPost("auto-fix/{scanResultId:int}")]
    public async Task<IActionResult> CreateAutoFixPr(int scanResultId, CancellationToken cancellationToken)
    {
        var prUrl = await _gitHubService.CreateAutoFixPullRequestAsync(scanResultId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { pullRequestUrl = prUrl }));
    }
}

public class GitHubWebhookPayload
{
    public string Action { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public int PullRequestNumber { get; set; }
}
