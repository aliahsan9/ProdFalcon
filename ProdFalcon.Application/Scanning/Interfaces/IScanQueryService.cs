using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IScanQueryService
{
    Task<PagedResult<ScanHistoryItemDto>> GetHistoryAsync(Guid? projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<ScanIssueSummaryDto>> GetFindingsAsync(FindingQuery query, CancellationToken cancellationToken = default);
    Task<ScanComparisonDto> CompareAsync(int baselineId, int currentId, CancellationToken cancellationToken = default);
    Task<ScanIssueSummaryDto> UpdateFindingStatusAsync(int issueId, string status, string? notes, CancellationToken cancellationToken = default);
    Task<ScanResultDto?> GetScanAsync(int scanResultId, CancellationToken cancellationToken = default);
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class ScanHistoryItemDto
{
    public int ScanResultId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? RepositoryUrl { get; set; }
    public string? Branch { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string StageMessage { get; set; } = string.Empty;
    public int Score { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public int IssueCount { get; set; }
    public int? PreviousScore { get; set; }
}

public class FindingQuery
{
    public string? Severity { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    /// <summary>security excludes quality and reliability findings. quality returns those findings. Empty returns every finding.</summary>
    public string? Scope { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class ScanComparisonDto
{
    public int BaselineId { get; set; }
    public int CurrentId { get; set; }
    public int BaselineScore { get; set; }
    public int CurrentScore { get; set; }
    public int ScoreChange { get; set; }
    public IReadOnlyList<ScanIssueSummaryDto> Introduced { get; set; } = [];
    public IReadOnlyList<ScanIssueSummaryDto> Resolved { get; set; } = [];
    public IReadOnlyList<ScanIssueSummaryDto> Unchanged { get; set; } = [];
}
