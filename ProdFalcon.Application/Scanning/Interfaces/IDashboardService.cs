namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDashboardDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanTrendDto>> GetTrendsAsync(CancellationToken cancellationToken = default);
}

public class DashboardSummaryDto
{
    public int TotalProjects { get; set; }
    public int TotalScans { get; set; }
    public int TotalIssues { get; set; }
    public double AverageScore { get; set; }

    /// <summary>Aggregated category health scores (0–100).</summary>
    public double AverageSecurityScore { get; set; }
    public double AverageMaintainabilityScore { get; set; }
    public double AveragePerformanceScore { get; set; }
    public double AverageProductionReadinessScore { get; set; }
    public double ArchitectureScore { get; set; }
    public double DocumentationScore { get; set; }
    public double TestCoverageScore { get; set; }
    public double TechnicalDebtScore { get; set; }
    public double CodeSmellsScore { get; set; }
    public double ComplexityScore { get; set; }
    public double BugRiskScore { get; set; }

    public int CriticalVulnerabilities { get; set; }
    public int HighVulnerabilities { get; set; }
    public int MediumVulnerabilities { get; set; }
    public int LowVulnerabilities { get; set; }
    public int InformationalFindings { get; set; }
    public int ResolvedIssues { get; set; }
    public int OpenIssues { get; set; }
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new();
    public Dictionary<string, int> IssuesByCategory { get; set; } = new();
    public Dictionary<string, int> TopViolatedRules { get; set; } = new();
    public IReadOnlyList<string> AiRecommendations { get; set; } = Array.Empty<string>();
}

public class ProjectDashboardDto
{
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int LatestScore { get; set; }
    public int LatestSecurityScore { get; set; }
    public int LatestMaintainabilityScore { get; set; }
    public int LatestPerformanceScore { get; set; }
    public int LatestProductionReadinessScore { get; set; }
    public int LatestScanResultId { get; set; }
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
    public string Trend { get; set; } = "Stable";
    public string SourceType { get; set; } = "Zip";
    public string? RepositoryUrl { get; set; }
    public string? Branch { get; set; }
    public string? DisplayName { get; set; }
    public int LatestDurationMs { get; set; }
    public string LatestStatus { get; set; } = string.Empty;
}

public class ScanTrendDto
{
    public DateTime Date { get; set; }
    public double AverageScore { get; set; }
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
}
