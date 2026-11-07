using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IProjectWorkspaceStore _workspace;
    private readonly ITenantProvider _tenantProvider;
    private readonly IScanProjectRepository _projects;
    private readonly IScanResultRepository _results;
    private readonly IScanService _scans;
    private readonly IProjectStorageService _storage;
    private readonly IScanQueue _queue;
    private readonly ILogger<GitHubController> _logger;

    public GitHubController(
        IConfiguration configuration,
        IProjectWorkspaceStore workspace,
        ITenantProvider tenantProvider,
        IScanProjectRepository projects,
        IScanResultRepository results,
        IScanService scans,
        IProjectStorageService storage,
        IScanQueue queue,
        ILogger<GitHubController> logger)
    {
        _configuration = configuration;
        _workspace = workspace;
        _tenantProvider = tenantProvider;
        _projects = projects;
        _results = results;
        _scans = scans;
        _storage = storage;
        _queue = queue;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("registrations")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _workspace.ListRegistrationsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(items.Select(item => new
        {
            item.Id,
            item.Owner,
            item.Name,
            item.Branch,
            item.TokenPrefix,
            item.FailOnNewCritical,
            item.WarnOnHigh,
            item.AllowAcceptedRisk,
            item.PreventScoreRegression,
            item.CreatedAt
        })));
    }

    [Authorize]
    [HttpPost("registrations")]
    public async Task<IActionResult> Register([FromBody] GitHubRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (_tenantProvider.Role == TenantRole.Viewer)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Viewers cannot register repositories."));

        if (!GitHubUrlParser.TryParse(request.RepositoryUrl, out var reference, out var error) || reference == null)
            return BadRequest(ApiResponse<object>.Fail(error));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var registration = await _workspace.AddRegistrationAsync(new RepositoryRegistration
        {
            Owner = reference.Owner,
            Name = reference.Name,
            Branch = string.IsNullOrWhiteSpace(request.Branch) ? reference.Branch : request.Branch.Trim(),
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            TokenPrefix = token[..8],
            FailOnNewCritical = request.FailOnNewCritical,
            WarnOnHigh = request.WarnOnHigh,
            AllowAcceptedRisk = request.AllowAcceptedRisk,
            PreventScoreRegression = request.PreventScoreRegression
        }, cancellationToken);

        var apiBase = $"{Request.Scheme}://{Request.Host}";
        return Ok(ApiResponse<object>.Ok(new
        {
            registration.Id,
            registration.Owner,
            registration.Name,
            token,
            workflow = GitHubWorkflowTemplate.Create(apiBase),
            message = "Store this token as the PRODFALCON_TOKEN secret. It is not shown again."
        }));
    }

    [Authorize]
    [HttpGet("workflow")]
    public IActionResult Workflow()
    {
        var yaml = GitHubWorkflowTemplate.Create($"{Request.Scheme}://{Request.Host}");
        return File(Encoding.UTF8.GetBytes(yaml), "text/yaml", GitHubWorkflowTemplate.FileName);
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var secret = _configuration["GitHub:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("GitHub:WebhookSecret is not configured."));

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (!GitHubWebhookVerifier.IsValid(body, Request.Headers["X-Hub-Signature-256"], secret))
            return Unauthorized(ApiResponse<object>.Fail("Webhook signature was rejected."));

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var action = root.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : null;
        if (action is not ("opened" or "synchronize" or "reopened"))
            return Ok(ApiResponse<object>.Ok(new { message = "Event ignored." }));

        if (!root.TryGetProperty("repository", out var repository) ||
            !repository.TryGetProperty("full_name", out var fullNameElement))
            return BadRequest(ApiResponse<object>.Fail("The payload has no repository."));

        var fullName = fullNameElement.GetString() ?? string.Empty;
        var parts = fullName.Split('/', 2);
        if (parts.Length != 2)
            return BadRequest(ApiResponse<object>.Fail("Repository name was not owner/name."));

        var registration = await _workspace.FindRegistrationByRepositoryAsync(parts[0], parts[1], cancellationToken);
        if (registration == null)
            return Ok(ApiResponse<object>.Ok(new { message = "Repository is not registered with ProdFalcon." }));

        var branch = registration.Branch;
        string? sha = null;
        if (root.TryGetProperty("pull_request", out var pullRequest))
        {
            if (pullRequest.TryGetProperty("head", out var head))
            {
                if (head.TryGetProperty("ref", out var refElement))
                    branch = refElement.GetString();
                if (head.TryGetProperty("sha", out var shaElement))
                    sha = shaElement.GetString();
            }
        }

        _tenantProvider.SetTenant(registration.TenantId, role: TenantRole.Owner);
        var project = new ScanProject
        {
            Id = Guid.NewGuid(),
            TenantId = registration.TenantId,
            FileName = fullName,
            DisplayName = fullName,
            UploadedAt = DateTime.UtcNow,
            Status = "Queued",
            SourceType = "GitHub",
            RepositoryUrl = $"https://github.com/{parts[0]}/{parts[1]}",
            Branch = branch,
            CommitSha = sha
        };
        await _projects.CreateAsync(project, cancellationToken);
        var queued = new ScanResult
        {
            TenantId = registration.TenantId,
            ScanProjectId = project.Id,
            CreatedAt = DateTime.UtcNow,
            Status = "Queued",
            Stage = "Queued",
            StageMessage = "Queued from a verified GitHub webhook.",
            CommitSha = sha
        };
        var saved = await _results.SaveAsync(queued, cancellationToken);
        await _queue.QueueAsync(new ScanWorkItem
        {
            TenantId = registration.TenantId,
            ProjectId = project.Id,
            ScanResultId = saved.Id
        }, cancellationToken);

        _logger.LogInformation("Queued webhook scan {ScanId} for {Repository}", saved.Id, fullName);
        return Accepted(ApiResponse<object>.Ok(new { scanResultId = saved.Id, status = "Queued" }));
    }

    [AllowAnonymous]
    [HttpPost("ci")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> ContinuousIntegration(IFormFile? file, [FromForm] string? branch, [FromForm] string? commitSha, [FromForm] string? changedFiles, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(ApiResponse<object>.Fail("A bearer scan token is required."));

        var token = header.ToString()["Bearer ".Length..].Trim();
        var registration = await _workspace.FindByTokenHashAsync(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), cancellationToken);
        if (registration == null)
            return Unauthorized(ApiResponse<object>.Fail("The scan token was not recognized."));

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Upload the repository ZIP produced by the workflow."));

        _tenantProvider.SetTenant(registration.TenantId, role: TenantRole.Owner);
        var projectId = Guid.NewGuid();
        await using var stream = file.OpenReadStream();
        var zipPath = await _storage.SaveZipAsync(projectId, stream, cancellationToken);
        if (!await _storage.ValidateZipAsync(zipPath, cancellationToken))
        {
            await _storage.CleanupProjectAsync(projectId, cancellationToken);
            return BadRequest(ApiResponse<object>.Fail("The archive is empty or invalid."));
        }

        var project = new ScanProject
        {
            Id = projectId,
            TenantId = registration.TenantId,
            FileName = $"{registration.Owner}/{registration.Name}",
            DisplayName = $"{registration.Owner}/{registration.Name}",
            UploadedAt = DateTime.UtcNow,
            Status = "Queued",
            SourceType = "Zip",
            RepositoryUrl = $"https://github.com/{registration.Owner}/{registration.Name}",
            Branch = string.IsNullOrWhiteSpace(branch) ? registration.Branch : branch,
            CommitSha = commitSha,
            ZipPath = zipPath
        };
        await _projects.CreateAsync(project, cancellationToken);
        var queued = await _results.SaveAsync(new ScanResult
        {
            TenantId = registration.TenantId,
            ScanProjectId = project.Id,
            CreatedAt = DateTime.UtcNow,
            Status = "Queued",
            Stage = "Queued",
            StageMessage = "CI scan started.",
            CommitSha = commitSha
        }, cancellationToken);

        var completed = await _scans.ExecuteQueuedScanAsync(project.Id, queued.Id, null, null, cancellationToken);
        var stored = await _results.GetByIdAsync(completed.ScanResultId, cancellationToken);
        var analysis = ProjectAnalysisPipeline.Read(stored?.AnalysisDocument);
        var decision = GitHubPolicyEvaluator.Evaluate(
            registration,
            analysis?.Comparison ?? new ScanComparison(),
            completed.OverallScore,
            completed.PreviousScore);

        var changed = (changedFiles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changedFindings = completed.Issues
            .Where(issue => changed.Count == 0 || changed.Any(path => issue.FilePath.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            .Take(30)
            .Select(issue => $"{issue.Severity} {issue.Title} {issue.FilePath}:{issue.LineNumber}")
            .ToList();

        return decision.Failed
            ? StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Ok(new
            {
                passed = false,
                completed.ScanResultId,
                completed.OverallScore,
                decision.NewCritical,
                decision.NewHigh,
                decision.Messages,
                changedFindings
            }))
            : Ok(ApiResponse<object>.Ok(new
            {
                passed = true,
                completed.ScanResultId,
                completed.OverallScore,
                decision.NewCritical,
                decision.NewHigh,
                decision.Messages,
                changedFindings
            }));
    }

    [Authorize]
    [HttpPost("auto-fix/{scanResultId:int}")]
    public IActionResult CreateAutoFixPr(int scanResultId) =>
        StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
            "Automatic pull requests are not implemented. ProdFalcon does not open a pull request or invent a URL."));
}

public class GitHubRegistrationRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public bool FailOnNewCritical { get; set; } = true;
    public bool WarnOnHigh { get; set; } = true;
    public bool AllowAcceptedRisk { get; set; } = true;
    public bool PreventScoreRegression { get; set; }
}
