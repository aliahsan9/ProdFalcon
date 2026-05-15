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
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new();
    public Dictionary<string, int> TopViolatedRules { get; set; } = new();
}

public class ProjectDashboardDto
{
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int LatestScore { get; set; }
    public int LatestScanResultId { get; set; }
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
    public string Trend { get; set; } = "Stable";
}

public class ScanTrendDto
{
    public DateTime Date { get; set; }
    public double AverageScore { get; set; }
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
}
