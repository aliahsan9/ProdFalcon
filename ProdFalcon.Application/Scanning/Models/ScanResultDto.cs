namespace ProdFalcon.Application.Scanning.Models;

public class ScanResultDto
{
    public Guid ProjectId { get; set; }
    public int ScanResultId { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public int SecurityScore { get; set; }
    public int MaintainabilityScore { get; set; }
    public int PerformanceScore { get; set; }
    public int ProductionReadinessScore { get; set; }
    public int TotalIssues { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string StageMessage { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int? PreviousScore { get; set; }
    public int? ScoreChange { get; set; }
    public int FileCount { get; set; }
    public IReadOnlyList<string> Technologies { get; set; } = [];
    public string SourceType { get; set; } = string.Empty;
    public string? RepositoryUrl { get; set; }
    public string? Branch { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ScoringNote { get; set; } = string.Empty;
    public IReadOnlyList<ScanIssueSummaryDto> Issues { get; set; } = [];
}

public class ScanIssueSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LineNumber { get; set; }

    public int ColumnNumber { get; set; }
    public string Confidence { get; set; } = "High";
    public string? Cwe { get; set; }
    public string? Cve { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
    public string SecureExample { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string DetectionMethod { get; set; } = string.Empty;
    public string References { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? FixSuggestion { get; set; }
}
