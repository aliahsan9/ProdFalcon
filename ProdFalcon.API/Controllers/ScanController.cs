using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ScanResultEntity = ProdFalcon.Application.Scanning.Models.ScanResult;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly IProjectStorageService _storage;
    private readonly IScanProjectRepository _projectRepository;
    private readonly IScanService _scanService;
    private readonly IScanResultRepository _scanResultRepository;
    private readonly ILogger<ScanController> _logger;

    public ScanController(
        IProjectStorageService storage,
        IScanProjectRepository projectRepository,
        IScanService scanService,
        IScanResultRepository scanResultRepository,
        ILogger<ScanController> logger)
    {
        _storage = storage;
        _projectRepository = projectRepository;
        _scanService = scanService;
        _scanResultRepository = scanResultRepository;
        _logger = logger;
    }

    [HttpGet("{scanResultId:int}")]
    public async Task<IActionResult> GetScan(int scanResultId, CancellationToken cancellationToken)
    {
        var result = await _scanResultRepository.GetByIdAsync(scanResultId, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<ScanResultDto>.Fail($"Scan result {scanResultId} not found."));

        return Ok(ApiResponse<ScanResultDto>.Ok(MapToDto(result)));
    }

    private static ScanResultDto MapToDto(ScanResultEntity result) =>
        new()
        {
            ProjectId = result.ScanProjectId,
            ScanResultId = result.Id,
            ProjectPath = result.ProjectPath,
            OverallScore = result.Score,
            SecurityScore = result.SecurityScore,
            MaintainabilityScore = result.MaintainabilityScore,
            PerformanceScore = result.PerformanceScore,
            ProductionReadinessScore = result.ProductionReadinessScore,
            TotalIssues = result.Issues.Count,
            Status = result.Status,
            DurationMs = result.DurationMs,
            Issues = result.Issues.Select(i => new ScanIssueSummaryDto
            {
                Title = i.Title,
                Severity = i.Severity,
                FilePath = i.FilePath,
                RuleName = i.RuleName,
                Category = i.Category
            }).ToList()
        };

    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)]
    [ProducesResponseType(typeof(ApiResponse<ScanUploadResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadZip(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("No ZIP file uploaded."));

        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail("Only ZIP files are allowed."));

        var projectId = Guid.NewGuid();
        string? zipPath = null;
        string? extractionPath = null;

        try
        {
            await using var uploadStream = file.OpenReadStream();
            zipPath = await _storage.SaveZipAsync(projectId, uploadStream, cancellationToken);

            if (!await _storage.ValidateZipAsync(zipPath, cancellationToken))
                return BadRequest(ApiResponse<ScanUploadResponse>.Fail("Invalid or empty ZIP archive."));

            extractionPath = await _storage.ExtractZipAsync(projectId, zipPath, cancellationToken);

            var project = new ScanProject
            {
                Id = projectId,
                FileName = file.FileName,
                UploadedAt = DateTime.UtcNow,
                Status = "Processing",
                ZipPath = zipPath,
                ExtractedPath = extractionPath
            };

            await _projectRepository.CreateAsync(project, cancellationToken);

            var scanResult = await _scanService.ScanProjectAsync(projectId, extractionPath, cancellationToken);

            var response = new ScanUploadResponse
            {
                ProjectId = projectId,
                StorageRoot = _storage.StorageRoot,
                UploadedZip = zipPath,
                ExtractedProject = extractionPath,
                Scan = scanResult
            };

            _logger.LogInformation(
                "Scan completed for project {ProjectId} with score {Score}",
                projectId,
                scanResult.OverallScore);

            return Ok(ApiResponse<ScanUploadResponse>.Ok(
                response,
                $"Your project is {scanResult.OverallScore}/100 production ready."));
        }
        catch (InvalidDataException ex)
        {
            await SafeCleanupAsync(projectId);
            return BadRequest(ApiResponse<ScanUploadResponse>.Fail($"Invalid ZIP file: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZIP scan failed for project {ProjectId}", projectId);
            await SafeCleanupAsync(projectId);
            throw;
        }
    }

    private async Task SafeCleanupAsync(Guid projectId)
    {
        try
        {
            await _storage.CleanupProjectAsync(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup storage for project {ProjectId}", projectId);
        }
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
