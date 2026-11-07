using System.Diagnostics;
using System.Text.Json;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Remediation;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Services;

public class ScanService : IScanService
{
    private readonly IScanResultRepository _scanResultRepository;
    private readonly IScanProjectRepository _scanProjectRepository;
    private readonly IScanRuleExecutor _ruleExecutor;
    private readonly IRiskScoringService _riskScoring;
    private readonly IRepositoryAcquisitionService _acquisition;
    private readonly IProjectStorageService _storage;
    private readonly IProjectWorkspaceStore _workspace;

    public ScanService(
        IScanResultRepository scanResultRepository,
        IScanProjectRepository scanProjectRepository,
        IScanRuleExecutor ruleExecutor,
        IRiskScoringService riskScoring,
        IRepositoryAcquisitionService acquisition,
        IProjectStorageService storage,
        IProjectWorkspaceStore workspace)
    {
        _scanResultRepository = scanResultRepository;
        _scanProjectRepository = scanProjectRepository;
        _ruleExecutor = ruleExecutor;
        _riskScoring = riskScoring;
        _acquisition = acquisition;
        _storage = storage;
        _workspace = workspace;
    }

    public Task<ScanResultDto> ScanProjectAsync(
        Guid projectId,
        string projectPath,
        CancellationToken cancellationToken = default) =>
        ExecuteQueuedScanAsync(projectId, scanResultId: 0, projectPath, accessToken: null, cancellationToken);

    public async Task<ScanResultDto> ExecuteQueuedScanAsync(
        Guid projectId,
        int scanResultId,
        string? projectPath,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var project = await _scanProjectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Scan project {projectId} was not found.");

        var stopwatch = Stopwatch.StartNew();
        ScanResult? result = null;

        try
        {
            if (scanResultId > 0)
            {
                result = await _scanResultRepository.GetByIdAsync(scanResultId, cancellationToken)
                    ?? throw new InvalidOperationException($"Scan {scanResultId} was not found.");

                if (string.Equals(result.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return ScanMappings.ToDto(result, project);
            }

            project.Status = "Scanning";
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);

            var extractPath = await PrepareSourcesAsync(project, result, accessToken, cancellationToken);
            await UpdateStageAsync(result, "Scanning", "Detecting technologies", "Identifying languages and frameworks.", cancellationToken);

            var technologies = ProjectTechnologyDetector.Detect(extractPath);
            var technologyJson = ProjectTechnologyDetector.ToJson(technologies);
            project.DetectedTechnologies = technologyJson;
            var fileCount = SourceTree.EnumerateSourceFiles(extractPath).Count();

            var modules = ScanMappings.ReadModules(result?.ConfigurationJson);
            var execution = await _ruleExecutor.ExecuteAllAsync(
                extractPath,
                cancellationToken,
                modules,
                async (ruleName, ct) =>
                {
                    await UpdateStageAsync(result, "Scanning", "Running scanners", $"Running {ruleName}.", ct);
                });

            await UpdateStageAsync(result, "Scanning", "Understanding project", "Building the project profile, architecture, and threat model.", cancellationToken);
            var supplemental = AnalyzeSafely(extractPath);
            var prepared = PrepareIssues(execution.Issues.Concat(supplemental.AdditionalIssues), extractPath, project.TenantId);
            var tracked = await _workspace.GetTrackedAsync(projectId, cancellationToken);
            var history = await _scanResultRepository.GetByProjectIdAsync(projectId, cancellationToken);
            var previousResult = history
                .Where(item => item.Id != result?.Id && item.Status == "Completed")
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();
            var comparison = FindingReconciler.Reconcile(
                tracked,
                prepared,
                previousResult?.Id ?? 0,
                previousResult?.ScanProject?.Branch ?? project.Branch,
                project.Branch,
                previousResult?.CommitSha,
                project.CommitSha,
                DateTime.UtcNow);
            foreach (var finding in tracked)
            {
                finding.TenantId = project.TenantId;
                finding.ScanProjectId = projectId;
            }

            var document = new ProjectAnalysisDocument
            {
                Intelligence = supplemental.Profile,
                Architecture = supplemental.Architecture,
                ThreatModel = supplemental.Threats,
                Comparison = comparison,
                UnverifiedAreas = supplemental.UnverifiedAreas
            };
            var analysisJson = JsonSerializer.Serialize(document, ProjectAnalysisPipeline.JsonOptions);

            await UpdateStageAsync(result, "Scanning", "Scoring", "Calculating the security score from open findings.", cancellationToken);
            var scores = _riskScoring.Calculate(prepared);
            var previous = previousResult?.Score;

            stopwatch.Stop();

            if (result == null)
            {
                result = new ScanResult
                {
                    TenantId = project.TenantId,
                    ScanProjectId = projectId,
                    CreatedAt = DateTime.UtcNow
                };
            }

            result.ProjectPath = extractPath;
            result.Score = scores.OverallScore;
            result.SecurityScore = scores.SecurityScore;
            result.MaintainabilityScore = scores.MaintainabilityScore;
            result.PerformanceScore = scores.PerformanceScore;
            result.ProductionReadinessScore = scores.ProductionReadinessScore;
            result.RiskLevel = scores.RiskLevel;
            result.DurationMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            result.Status = "Completed";
            result.Stage = "Completed";
            result.StageMessage = "Scan finished.";
            result.Technologies = technologyJson;
            result.FileCount = fileCount;
            result.PreviousScore = previous;
            result.CommitSha = project.CommitSha;
            result.AnalysisDocument = analysisJson;
            result.ErrorMessage = null;
            if (result.Id == 0)
            {
                result.Issues = prepared;
            }
            else
            {
                result.Issues.Clear();
                result.Issues.AddRange(prepared);
            }

            ScanResult saved;
            if (result.Id == 0)
                saved = await _scanResultRepository.SaveAsync(result, cancellationToken);
            else
                saved = await _scanResultRepository.UpdateGraphAsync(result, cancellationToken);

            foreach (var finding in tracked)
            {
                var seen = prepared.Any(issue => issue.Fingerprint.Equals(finding.Fingerprint, StringComparison.OrdinalIgnoreCase));
                if (!seen)
                    continue;

                finding.LastScanResultId = saved.Id;
                if (finding.FirstScanResultId == 0)
                    finding.FirstScanResultId = saved.Id;
            }

            await _workspace.SaveTrackedAsync(tracked, cancellationToken);
            project.Status = "Completed";
            project.ExtractedPath = extractPath;
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);
            return ScanMappings.ToDto(saved, project);
        }
        catch (OperationCanceledException)
        {
            await MarkTerminalAsync(project, result, "Cancelled", "Cancelled", "The scan was cancelled or exceeded the time limit.");
            throw;
        }
        catch (Exception ex)
        {
            await MarkTerminalAsync(project, result, "Failed", "Failed", ex.Message);
            throw;
        }
    }

    private async Task<string> PrepareSourcesAsync(
        ScanProject project,
        ScanResult? result,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (string.Equals(project.SourceType, "GitHub", StringComparison.OrdinalIgnoreCase))
        {
            await UpdateStageAsync(result, "Scanning", "Acquiring repository", "Downloading the repository archive.", cancellationToken);
            if (!GitHubUrlParser.TryParse(project.RepositoryUrl, out var reference, out var error) || reference == null)
                throw new InvalidOperationException(error);

            var acquired = await _acquisition.AcquireGitHubAsync(
                project.Id,
                reference,
                project.Branch,
                accessToken,
                cancellationToken);

            project.ZipPath = acquired.ZipPath;
            project.ExtractedPath = acquired.ExtractedPath;
            project.Branch = acquired.Branch;
            project.CommitSha = acquired.CommitSha;
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);
            return acquired.ExtractedPath;
        }

        await UpdateStageAsync(result, "Scanning", "Extracting archive", "Validating and extracting the ZIP archive.", cancellationToken);
        if (string.IsNullOrWhiteSpace(project.ExtractedPath) || !Directory.Exists(project.ExtractedPath))
        {
            if (string.IsNullOrWhiteSpace(project.ZipPath) || !File.Exists(project.ZipPath))
                throw new InvalidOperationException("The uploaded archive is no longer available.");

            project.ExtractedPath = await _storage.ExtractZipAsync(project.Id, project.ZipPath, cancellationToken);
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);
        }

        return project.ExtractedPath;
    }

    private static ProjectAnalysis AnalyzeSafely(string projectPath)
    {
        try
        {
            return ProjectAnalysisPipeline.Analyze(projectPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ProjectAnalysis
            {
                UnverifiedAreas = ["Project intelligence failed: " + ex.Message]
            };
        }
    }

    private static List<ScanIssue> PrepareIssues(IEnumerable<ScanIssue> issues, string root, Guid tenantId)
    {
        var unique = new Dictionary<string, ScanIssue>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            issue.TenantId = tenantId;
            var relative = ToRelative(root, issue.FilePath);
            issue.FilePath = relative.Length <= 1024 ? relative : relative[^1024..];
            issue.Status = string.IsNullOrWhiteSpace(issue.Status) ? "Open" : issue.Status;
            issue.Confidence = string.IsNullOrWhiteSpace(issue.Confidence) ? "High" : issue.Confidence;
            issue.Fingerprint = FindingFingerprint.Create(issue.RuleId, issue.FilePath, issue.LineNumber, issue.Title);
            if (!unique.ContainsKey(issue.Fingerprint))
                unique[issue.Fingerprint] = issue;
        }

        return unique.Values.ToList();
    }

    private static string ToRelative(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            var relative = Path.GetRelativePath(root, path);
            if (relative.StartsWith("..", StringComparison.Ordinal))
                return path.Replace('\\', '/');

            return relative.Replace('\\', '/');
        }
        catch (Exception)
        {
            return path.Replace('\\', '/');
        }
    }

    private async Task UpdateStageAsync(ScanResult? result, string status, string stage, string message, CancellationToken cancellationToken)
    {
        if (result == null || result.Id == 0)
            return;

        await _scanResultRepository.UpdateStageAsync(result.Id, status, stage, message, cancellationToken);
    }

    private async Task MarkTerminalAsync(ScanProject project, ScanResult? result, string status, string stage, string message)
    {
        try
        {
            project.Status = status;
            await _scanProjectRepository.UpdateAsync(project);
            if (result is { Id: > 0 })
            {
                await _scanResultRepository.UpdateStageAsync(result.Id, status, stage, message, CancellationToken.None);
                if (status == "Failed")
                    await _scanResultRepository.SetErrorAsync(result.Id, Trim(message), CancellationToken.None);
            }
        }
        catch
        {
            // The original failure is more useful than a status-write failure.
        }
    }

    private static string Trim(string message) =>
        message.Length <= 1800 ? message : message[..1800];
}
