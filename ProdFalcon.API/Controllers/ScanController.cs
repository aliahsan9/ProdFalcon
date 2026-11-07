using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Services;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Reporting;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly IProjectStorageService _storage;
    private readonly IScanProjectRepository _projectRepository;
    private readonly IScanResultRepository _scanResultRepository;
    private readonly IScanQueryService _queryService;
    private readonly IProjectWorkspaceStore _workspace;
    private readonly IScanQueue _queue;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditService _auditService;
    private readonly ScanLimitsOptions _limits;
    private readonly ILogger<ScanController> _logger;

    public ScanController(
        IProjectStorageService storage,
        IScanProjectRepository projectRepository,
        IScanResultRepository scanResultRepository,
        IScanQueryService queryService,
        IProjectWorkspaceStore workspace,
        IScanQueue queue,
        ITenantProvider tenantProvider,
        IAuditService auditService,
        IOptions<ScanLimitsOptions> limits,
        ILogger<ScanController> logger)
    {
        _storage = storage;
        _projectRepository = projectRepository;
        _scanResultRepository = scanResultRepository;
        _queryService = queryService;
        _workspace = workspace;
        _queue = queue;
        _tenantProvider = tenantProvider;
        _auditService = auditService;
        _limits = limits.Value;
        _logger = logger;
    }

    [HttpGet("{scanResultId:int}")]
    public async Task<IActionResult> GetScan(int scanResultId, CancellationToken cancellationToken)
    {
        var result = await _queryService.GetScanAsync(scanResultId, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<ScanResultDto>.Fail("Scan result not found."));

        return Ok(ApiResponse<ScanResultDto>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] Guid? projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var history = await _queryService.GetHistoryAsync(projectId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<ScanHistoryItemDto>>.Ok(history));
    }

    [HttpGet("findings")]
    public async Task<IActionResult> Findings(
        [FromQuery] string? severity,
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var findings = await _queryService.GetFindingsAsync(new FindingQuery
        {
            Severity = severity,
            Status = status,
            Category = category,
            Scope = scope,
            ProjectId = projectId,
            Search = q,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(ApiResponse<PagedResult<ScanIssueSummaryDto>>.Ok(findings));
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] int baselineId, [FromQuery] int currentId, CancellationToken cancellationToken)
    {
        try
        {
            var comparison = await _queryService.CompareAsync(baselineId, currentId, cancellationToken);
            return Ok(ApiResponse<ScanComparisonDto>.Ok(comparison));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ScanComparisonDto>.Fail(ex.Message));
        }
    }

    [HttpPatch("findings/{issueId:int}")]
    public async Task<IActionResult> UpdateFinding(int issueId, [FromBody] FindingStatusRequest request, CancellationToken cancellationToken)
    {
        if (_tenantProvider.Role == TenantRole.Viewer)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ScanIssueSummaryDto>.Fail("Viewers cannot change findings."));

        try
        {
            var updated = await _queryService.UpdateFindingStatusAsync(issueId, request.Status, request.Notes, cancellationToken);
            return Ok(ApiResponse<ScanIssueSummaryDto>.Ok(updated, "Finding status saved."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<ScanIssueSummaryDto>.Fail("Finding was not found."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ScanIssueSummaryDto>.Fail(ex.Message));
        }
    }

    [EnableRateLimiting("scans")]
    [HttpPost("upload")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> UploadZip(IFormFile file, [FromForm] string? configuration, CancellationToken cancellationToken)
    {
        if (_tenantProvider.Role == TenantRole.Viewer)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ScanUploadResponse>.Fail("Viewers cannot upload projects."));

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("No ZIP file uploaded."));

        if (file.Length > _limits.MaxUploadBytes)
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("The ZIP file exceeds the upload size limit."));

        if (!Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("Only ZIP files are allowed."));

        var projectId = Guid.NewGuid();
        try
        {
            await using var uploadStream = file.OpenReadStream();
            var zipPath = await _storage.SaveZipAsync(projectId, uploadStream, cancellationToken);
            if (!await _storage.ValidateZipAsync(zipPath, cancellationToken))
            {
                await _storage.CleanupProjectAsync(projectId, cancellationToken);
                return BadRequest(ApiResponse<ScanUploadResponse>.Fail("Invalid or empty ZIP archive."));
            }

            var project = new ScanProject
            {
                Id = projectId,
                FileName = Path.GetFileName(file.FileName),
                DisplayName = Path.GetFileNameWithoutExtension(file.FileName),
                UploadedAt = DateTime.UtcNow,
                Status = "Queued",
                SourceType = "Zip",
                ZipPath = zipPath,
                UserId = _tenantProvider.UserId
            };

            await _projectRepository.CreateAsync(project, cancellationToken);
            var queued = await QueueAsync(project, configuration, accessToken: null, cancellationToken);
            await _auditService.LogAsync(_tenantProvider.TenantId, _tenantProvider.UserId, "ProjectUploaded", $"{{\"projectId\":\"{projectId}\"}}", cancellationToken);
            return Ok(ApiResponse<ScanUploadResponse>.Ok(new ScanUploadResponse
            {
                ProjectId = projectId,
                Scan = queued,
                UploadedZip = $"{_tenantProvider.TenantId:N}/{projectId:N}/upload.zip"
            }, "Scan queued."));
        }
        catch (InvalidDataException ex)
        {
            await _storage.CleanupProjectAsync(projectId);
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail(ex.Message));
        }
    }

    [EnableRateLimiting("scans")]
    [HttpPost("github")]
    public async Task<IActionResult> ScanGitHub([FromBody] GitHubScanRequest request, CancellationToken cancellationToken)
    {
        if (_tenantProvider.Role == TenantRole.Viewer)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ScanUploadResponse>.Fail("Viewers cannot start scans."));

        if (!GitHubUrlParser.TryParse(request.RepositoryUrl, out var reference, out var error) || reference == null)
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail(error));

        if (!string.IsNullOrWhiteSpace(request.Branch) && !GitHubUrlParser.IsValidBranch(request.Branch))
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("The branch name is not valid."));

        var project = new ScanProject
        {
            Id = Guid.NewGuid(),
            FileName = $"{reference.Owner}/{reference.Name}",
            DisplayName = $"{reference.Owner}/{reference.Name}",
            UploadedAt = DateTime.UtcNow,
            Status = "Queued",
            SourceType = "GitHub",
            RepositoryUrl = $"https://github.com/{reference.Owner}/{reference.Name}",
            Branch = string.IsNullOrWhiteSpace(request.Branch) ? reference.Branch : request.Branch.Trim(),
            UserId = _tenantProvider.UserId
        };

        await _projectRepository.CreateAsync(project, cancellationToken);
        var queued = await QueueAsync(project, request.Configuration == null ? null : JsonSerializer.Serialize(request.Configuration), request.AccessToken, cancellationToken);
        _logger.LogInformation("Queued GitHub scan {ProjectId} for {Repository}", project.Id, project.RepositoryUrl);
        return Ok(ApiResponse<ScanUploadResponse>.Ok(new ScanUploadResponse { ProjectId = project.Id, Scan = queued }, "Repository scan queued."));
    }

    [HttpPost("{scanResultId:int}/cancel")]
    public async Task<IActionResult> Cancel(int scanResultId, CancellationToken cancellationToken)
    {
        var scan = await _queryService.GetScanAsync(scanResultId, cancellationToken);
        if (scan == null)
            return NotFound(ApiResponse<object>.Fail("Scan result not found."));

        if (scan.Status is "Completed" or "Failed" or "Cancelled")
            return BadRequest(ApiResponse<object>.Fail("This scan is already finished."));

        _queue.TryCancel(scanResultId);
        await _scanResultRepository.UpdateStageAsync(scanResultId, "Cancelled", "Cancelled", "Cancellation requested.", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { scanResultId, status = "Cancelled" }));
    }

    [HttpGet("{scanResultId:int}/report")]
    public async Task<IActionResult> Report(int scanResultId, [FromQuery] string format = "json", CancellationToken cancellationToken = default)
    {
        var scan = await _queryService.GetScanAsync(scanResultId, cancellationToken);
        if (scan == null)
            return NotFound(ApiResponse<object>.Fail("Scan result not found."));

        var name = string.IsNullOrWhiteSpace(scan.ProjectName) ? $"scan-{scanResultId}" : scan.ProjectName;
        return format.ToLowerInvariant() switch
        {
            "csv" => File(ScanReportWriter.WriteCsv(scan), "text/csv", $"{name}-security.csv"),
            "pdf" => File(ScanReportWriter.WritePdf(scan), "application/pdf", $"{name}-security.pdf"),
            _ => File(ScanReportWriter.WriteJson(scan), "application/json", $"{name}-security.json")
        };
    }

    [HttpGet("{scanResultId:int}/intelligence")]
    public async Task<IActionResult> Intelligence(int scanResultId, CancellationToken cancellationToken)
    {
        var document = await ReadAnalysisAsync(scanResultId, cancellationToken);
        if (document == null)
            return NotFound(ApiResponse<object>.Fail("This scan has no project intelligence profile."));

        return Ok(ApiResponse<ProjectIntelligenceProfile>.Ok(document.Intelligence));
    }

    [HttpGet("{scanResultId:int}/architecture")]
    public async Task<IActionResult> Architecture(int scanResultId, CancellationToken cancellationToken)
    {
        var document = await ReadAnalysisAsync(scanResultId, cancellationToken);
        if (document == null)
            return NotFound(ApiResponse<object>.Fail("This scan has no architecture graph."));

        return Ok(ApiResponse<ArchitectureGraph>.Ok(document.Architecture));
    }

    [HttpGet("{scanResultId:int}/threats")]
    public async Task<IActionResult> Threats(int scanResultId, CancellationToken cancellationToken)
    {
        var document = await ReadAnalysisAsync(scanResultId, cancellationToken);
        if (document == null)
            return NotFound(ApiResponse<object>.Fail("This scan has no threat model."));

        return Ok(ApiResponse<ThreatModelDocument>.Ok(document.ThreatModel));
    }

    [HttpGet("{scanResultId:int}/conversation")]
    public async Task<IActionResult> Conversation(int scanResultId, CancellationToken cancellationToken)
    {
        var scan = await _queryService.GetScanAsync(scanResultId, cancellationToken);
        if (scan == null)
            return NotFound(ApiResponse<object>.Fail("Scan result not found."));

        var messages = await _workspace.GetCopilotMessagesAsync(scanResultId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(messages.Select(message => new { message.Role, message.Content, message.CreatedAt })));
    }

    private async Task<ProjectAnalysisDocument?> ReadAnalysisAsync(int scanResultId, CancellationToken cancellationToken)
    {
        var scan = await _queryService.GetScanAsync(scanResultId, cancellationToken);
        if (scan == null)
            return null;

        var stored = await _scanResultRepository.GetByIdAsync(scanResultId, cancellationToken);
        return ProjectAnalysisPipeline.Read(stored?.AnalysisDocument);
    }

    [HttpGet("scoring")]
    public IActionResult Scoring() =>
        Ok(ApiResponse<object>.Ok(new { methodology = SecurityScoreMethodology.Summary }));

    private async Task<ScanResultDto> QueueAsync(ScanProject project, string? configuration, string? accessToken, CancellationToken cancellationToken)
    {
        var result = new ScanResult
        {
            TenantId = project.TenantId,
            ScanProjectId = project.Id,
            ProjectPath = project.ExtractedPath,
            CreatedAt = DateTime.UtcNow,
            Status = "Queued",
            Stage = "Queued",
            StageMessage = "Waiting for a scan worker.",
            ConfigurationJson = string.IsNullOrWhiteSpace(configuration) ? "{\"fullAudit\":true}" : configuration
        };

        var saved = await _scanResultRepository.SaveAsync(result, cancellationToken);
        await _queue.QueueAsync(new ScanWorkItem
        {
            TenantId = _tenantProvider.TenantId,
            UserId = _tenantProvider.UserId,
            ProjectId = project.Id,
            ScanResultId = saved.Id,
            GitHubAccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken
        }, cancellationToken);

        return ScanMappings.ToDto(saved, project);
    }
}

public class ScanUploadResponse
{
    public Guid ProjectId { get; set; }
    public string StorageRoot { get; set; } = string.Empty;
    public string UploadedZip { get; set; } = string.Empty;
    public string ExtractedProject { get; set; } = string.Empty;
    public ScanResultDto Scan { get; set; } = new();
}

public class GitHubScanRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? AccessToken { get; set; }
    public ScanModuleSelection? Configuration { get; set; }
}

public class FindingStatusRequest
{
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
}
