using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Infrastructure.Services;

public interface IGitHubIntegrationService
{
    Task<ScanResultDto> AnalyzePullRequestAsync(string repository, int pullRequestNumber, CancellationToken cancellationToken = default);
    Task<string> CreateAutoFixPullRequestAsync(int scanResultId, CancellationToken cancellationToken = default);
}

public class GitHubIntegrationService : IGitHubIntegrationService
{
    private readonly IScanService _scanService;
    private readonly ILogger<GitHubIntegrationService> _logger;

    public GitHubIntegrationService(IScanService scanService, ILogger<GitHubIntegrationService> logger)
    {
        _scanService = scanService;
        _logger = logger;
    }

    public Task<ScanResultDto> AnalyzePullRequestAsync(string repository, int pullRequestNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GitHub PR analysis stub for {Repository} PR #{PullRequestNumber}",
            repository,
            pullRequestNumber);

        throw new NotImplementedException(
            "Pull request analysis runs through the GitHub Actions workflow and POST /api/github/ci. This method does not clone a repository.");
    }

    public Task<string> CreateAutoFixPullRequestAsync(int scanResultId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Auto-fix was requested for scan result {ScanResultId} and refused", scanResultId);
        throw new NotImplementedException(
            "Automatic pull requests are not implemented. ProdFalcon does not open a pull request or invent a URL.");
    }
}
