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
using ProdFalcon.API.Authorization;
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
    private readonly IGitHubConnectionService _connections;
    private readonly ISecurityPolicyService _policies;
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
        IGitHubConnectionService connections,
        ISecurityPolicyService policies,
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
        _connections = connections;
        _policies = policies;
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

    [Authorize(Policy = TenantPolicies.CanAdminister)]
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
    [HttpGet("connection")]
    public async Task<IActionResult> Connection(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _connections.GetStatusAsync(cancellationToken);
            return Ok(ApiResponse<GitHubConnectionDto>.Ok(status));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<GitHubConnectionDto>.Fail(ex.Message));
        }
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpGet("oauth/start")]
    public IActionResult StartOAuth()
    {
        try
        {
            var url = _connections.BuildAuthorizeUrl(OAuthCallbackUrl());
            return Ok(ApiResponse<object>.Ok(new { url }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var frontend = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Redirect($"{frontend}/integrations?github=denied");

        try
        {
            await _connections.CompleteOAuthAsync(code, state, OAuthCallbackUrl(), cancellationToken);
            return Redirect($"{frontend}/integrations?github=connected");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            _logger.LogInformation(ex, "GitHub OAuth callback was rejected");
            return Redirect($"{frontend}/integrations?github=error");
        }
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpPost("connection")]
    public async Task<IActionResult> ConnectToken([FromBody] GitHubTokenConnectRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _connections.ConnectTokenAsync(request.AccessToken, cancellationToken);
            return Ok(ApiResponse<GitHubConnectionDto>.Ok(status, $"Connected as {status.Login}."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GitHubConnectionDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<GitHubConnectionDto>.Fail(ex.Message));
        }
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpDelete("connection")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        try
        {
            await _connections.DisconnectAsync(cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { connected = false }, "GitHub disconnected."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpGet("repos")]
    public async Task<IActionResult> Repositories(CancellationToken cancellationToken)
    {
        try
        {
            var repos = await _connections.ListRepositoriesAsync(cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<GitHubRepositoryDto>>.Ok(repos));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IReadOnlyList<GitHubRepositoryDto>>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<IReadOnlyList<GitHubRepositoryDto>>.Fail(ex.Message));
        }
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpPost("repos/scan")]
    public async Task<IActionResult> ScanConnectedRepository([FromBody] ConnectedRepositoryScanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var queued = await _connections.ScanRepositoryAsync(request.Owner, request.Name, request.Branch, cancellationToken);
            return Ok(ApiResponse<QueuedRepositoryScanDto>.Ok(queued, "Repository scan queued."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<QueuedRepositoryScanDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<QueuedRepositoryScanDto>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("pulls")]
    public async Task<IActionResult> PullRequests(CancellationToken cancellationToken)
    {
        var pulls = await _connections.ListPullRequestsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PullRequestScanDto>>.Ok(pulls));
    }

    [Authorize(Policy = TenantPolicies.CanScan)]
    [HttpPost("pulls/scan")]
    public async Task<IActionResult> ScanPullRequest([FromBody] PullRequestScanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var pull = await _connections.QueuePullRequestScanAsync(request.Owner, request.Name, request.Number, cancellationToken);
            return Ok(ApiResponse<PullRequestScanDto>.Ok(pull, "Pull request scan queued."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PullRequestScanDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PullRequestScanDto>.Fail(ex.Message));
        }
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

        if (root.TryGetProperty("pull_request", out var recordedPull)
            && recordedPull.TryGetProperty("number", out var numberElement)
            && numberElement.TryGetInt32(out var pullNumber))
        {
            var title = recordedPull.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            try
            {
                await _connections.RecordPullRequestAsync(new PullRequestRecord
                {
                    Owner = parts[0],
                    Name = parts[1],
                    Number = pullNumber,
                    Title = title ?? $"Pull request {pullNumber}",
                    HeadSha = sha,
                    Branch = branch,
                    Status = "Queued",
                    ScanResultId = saved.Id,
                    Summary = "Queued from a verified GitHub pull request webhook.",
                    Source = "Webhook"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pull request scan row was not saved for {Repository}#{Number}", fullName, pullNumber);
            }
        }

        _logger.LogInformation("Queued webhook scan {ScanId} for {Repository}", saved.Id, fullName);
        return Accepted(ApiResponse<object>.Ok(new { scanResultId = saved.Id, status = "Queued" }));
    }

    [AllowAnonymous]
    [HttpPost("ci")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> ContinuousIntegration(IFormFile? file, [FromForm] string? branch, [FromForm] string? commitSha, [FromForm] string? changedFiles, [FromForm] int? pullNumber, CancellationToken cancellationToken)
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

        var comparison = analysis?.Comparison ?? new ScanComparison();
        var newSecrets = comparison.Items.Count(item =>
            (item.Change is "New" or "Recurring" || (comparison.PreviousScanResultId == null && item.Change == "Detected"))
            && QualityGateEvaluator.LooksLikeSecret(item.Title, null, null));
        var openIssues = completed.Issues.Where(issue => QualityGateEvaluator.IsOpen(issue.Status)).ToList();
        var policy = await _policies.GetAsync(cancellationToken);
        var gate = QualityGateEvaluator.Evaluate(policy.ToSettings(), new QualityGateFacts(
            completed.OverallScore,
            completed.SecurityScore,
            completed.PreviousScore,
            openIssues.Count(issue => issue.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)),
            openIssues.Count(issue => issue.Severity.Equals("High", StringComparison.OrdinalIgnoreCase)),
            decision.NewCritical,
            decision.NewHigh,
            newSecrets));
        var passed = !decision.Failed && gate.Passed;
        var messages = decision.Messages.Concat(gate.Messages).Distinct().ToList();

        if (pullNumber is > 0)
        {
            try
            {
                await _connections.RecordPullRequestAsync(new PullRequestRecord
                {
                    Owner = registration.Owner,
                    Name = registration.Name,
                    Number = pullNumber.Value,
                    Title = $"Pull request {pullNumber.Value}",
                    HeadSha = commitSha,
                    Branch = project.Branch,
                    Status = passed ? "Passed" : "Failed",
                    ScanResultId = completed.ScanResultId,
                    Summary = string.Join(" ", messages),
                    Source = "Actions"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CI pull request row was not saved for {Owner}/{Name}#{Number}", registration.Owner, registration.Name, pullNumber);
            }
        }

        var payload = new
        {
            passed,
            completed.ScanResultId,
            completed.OverallScore,
            decision.NewCritical,
            decision.NewHigh,
            messages,
            changedFindings,
            qualityGate = new { passed = gate.Passed, gate.Messages }
        };

        return passed
            ? Ok(ApiResponse<object>.Ok(payload))
            : StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Ok(payload));
    }

    private string OAuthCallbackUrl() => $"{Request.Scheme}://{Request.Host}/api/github/oauth/callback";

    [Authorize]
    [HttpPost("auto-fix/{scanResultId:int}")]
    public IActionResult CreateAutoFixPr(int scanResultId) =>
        StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
            "Automatic pull requests are not implemented. ProdFalcon does not open a pull request or invent a URL."));
}

public class GitHubTokenConnectRequest
{
    public string AccessToken { get; set; } = string.Empty;
}

public class ConnectedRepositoryScanRequest
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Branch { get; set; }
}

public class PullRequestScanRequest
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Number { get; set; }
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
