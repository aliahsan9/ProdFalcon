using System.Diagnostics;
using System.Text.Json;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Services;

public static class ScanMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ScanResultDto ToDto(ScanResult result, ScanProject? project = null)
    {
        IReadOnlyList<string> technologies = [];
        if (!string.IsNullOrWhiteSpace(result.Technologies))
        {
            try
            {
                technologies = JsonSerializer.Deserialize<List<string>>(result.Technologies) ?? [];
            }
            catch (JsonException)
            {
                technologies = [];
            }
        }

        return new ScanResultDto
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
            Stage = result.Stage,
            StageMessage = result.StageMessage,
            RiskLevel = result.RiskLevel,
            PreviousScore = result.PreviousScore,
            ScoreChange = result.PreviousScore.HasValue ? result.Score - result.PreviousScore.Value : null,
            FileCount = result.FileCount,
            Technologies = technologies,
            SourceType = project?.SourceType ?? string.Empty,
            RepositoryUrl = project?.RepositoryUrl,
            Branch = project?.Branch,
            ProjectName = project?.DisplayName ?? project?.FileName ?? string.Empty,
            ScoringNote = SecurityScoreMethodology.Summary,
            Issues = result.Issues.Select(ToIssue).ToList()
        };
    }

    public static ScanIssueSummaryDto ToIssue(ScanIssue issue) =>
        new()
        {
            Id = issue.Id,
            Title = issue.Title,
            Severity = issue.Severity,
            FilePath = issue.FilePath,
            RuleName = issue.RuleName,
            RuleId = issue.RuleId,
            Category = issue.Category,
            Description = issue.Description,
            LineNumber = issue.LineNumber,
            Confidence = issue.Confidence,
            Cwe = issue.Cwe,
            Cve = issue.Cve,
            Evidence = issue.Evidence,
            Impact = issue.Impact,
            Remediation = issue.Remediation,
            SecureExample = issue.SecureExample,
            Status = string.IsNullOrWhiteSpace(issue.Status) ? "Open" : issue.Status,
            DetectionMethod = issue.DetectionMethod,
            References = issue.References,
            Notes = issue.Notes,
            FixSuggestion = string.IsNullOrWhiteSpace(issue.Remediation) ? null : issue.Remediation
        };

    public static ScanModuleSelection ReadModules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ScanModuleSelection();

        try
        {
            return JsonSerializer.Deserialize<ScanModuleSelection>(json, JsonOptions) ?? new ScanModuleSelection();
        }
        catch (JsonException)
        {
            return new ScanModuleSelection();
        }
    }
}
