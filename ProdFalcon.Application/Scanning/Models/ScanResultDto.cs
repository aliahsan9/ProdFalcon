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
    public IReadOnlyList<ScanIssueSummaryDto> Issues { get; set; } = [];
}

public class ScanIssueSummaryDto
{
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
