using System.Diagnostics;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Services;

public class ScanService : IScanService
{
    private readonly IScanResultRepository _scanResultRepository;
    private readonly IScanProjectRepository _scanProjectRepository;
    private readonly IScanRuleExecutor _ruleExecutor;
    private readonly IRiskScoringService _riskScoring;

    public ScanService(
        IScanResultRepository scanResultRepository,
        IScanProjectRepository scanProjectRepository,
        IScanRuleExecutor ruleExecutor,
        IRiskScoringService riskScoring)
    {
        _scanResultRepository = scanResultRepository;
        _scanProjectRepository = scanProjectRepository;
        _ruleExecutor = ruleExecutor;
        _riskScoring = riskScoring;
    }

    public async Task<ScanResultDto> ScanProjectAsync(
        Guid projectId,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = await _scanProjectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Scan project {projectId} was not found.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            project.Status = "Scanning";
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);

            var execution = await _ruleExecutor.ExecuteAllAsync(projectPath, cancellationToken);
            var scores = _riskScoring.Calculate(execution.Issues);

            stopwatch.Stop();

            var result = new ScanResult
            {
                TenantId = project.TenantId,
                ScanProjectId = projectId,
                ProjectPath = projectPath,
                CreatedAt = DateTime.UtcNow,
                Score = scores.OverallScore,
                SecurityScore = scores.SecurityScore,
                MaintainabilityScore = scores.MaintainabilityScore,
                PerformanceScore = scores.PerformanceScore,
                ProductionReadinessScore = scores.ProductionReadinessScore,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Status = "Completed",
                Issues = execution.Issues.Select(i => new ScanIssue
                {
                    TenantId = project.TenantId,
                    LineNumber = i.LineNumber,
                    Description = string.IsNullOrWhiteSpace(i.Description) ? "No description provided" : i.Description,
                    RuleId = string.IsNullOrWhiteSpace(i.RuleId) ? "unknown" : i.RuleId,
                    RuleName = string.IsNullOrWhiteSpace(i.RuleName) ? "unknown" : i.RuleName,
                    Title = string.IsNullOrWhiteSpace(i.Title) ? "Issue detected" : i.Title,
                    Severity = string.IsNullOrWhiteSpace(i.Severity) ? "Info" : i.Severity,
                    FilePath = i.FilePath,
                    Category = string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category
                }).ToList()
            };

            var saved = await _scanResultRepository.SaveAsync(result, cancellationToken);

            project.Status = "Completed";
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);

            return MapToDto(saved);
        }
        catch
        {
            project.Status = "Failed";
            await _scanProjectRepository.UpdateAsync(project, cancellationToken);
            throw;
        }
    }

    private static ScanResultDto MapToDto(ScanResult result) =>
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
}
