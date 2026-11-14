using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IGitHubConnectionService _pullRequests;

    public AnalyticsService(ApplicationDbContext db, ITenantProvider tenant, IGitHubConnectionService pullRequests)
    {
        _db = db;
        _tenant = tenant;
        _pullRequests = pullRequests;
    }

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(int days, CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Not authenticated.");

        days = Math.Clamp(days, 7, 365);
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var scans = await _db.ScanResults
            .AsNoTracking()
            .Where(item => item.CreatedAt >= since)
            .Select(item => new ScanFact(
                item.Id,
                item.ScanProjectId,
                item.CreatedAt,
                item.Score,
                item.SecurityScore,
                item.Status,
                item.PreviousScore))
            .ToListAsync(cancellationToken);

        var scanIds = scans.Select(item => item.Id).ToList();
        var issues = scanIds.Count == 0
            ? []
            : await _db.ScanIssues.AsNoTracking()
                .Where(item => scanIds.Contains(item.ScanResultId))
                .Select(item => new IssueFact(item.ScanResultId, item.Severity, item.Status))
                .ToListAsync(cancellationToken);

        var points = scans
            .GroupBy(item => item.CreatedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var ids = group.Select(item => item.Id).ToHashSet();
                var related = issues.Where(item => ids.Contains(item.ScanResultId) && IsOpen(item.Status)).ToList();
                return new AnalyticsPointDto
                {
                    Day = group.Key,
                    Scans = group.Count(),
                    AverageScore = Math.Round(group.Average(item => item.Score), 1),
                    OpenCritical = related.Count(item => IsSeverity(item.Severity, "Critical")),
                    OpenHigh = related.Count(item => IsSeverity(item.Severity, "High"))
                };
            })
            .ToList();

        var openIssues = issues.Where(item => IsOpen(item.Status)).ToList();
        var orderedScores = scans.OrderBy(item => item.CreatedAt).Select(item => item.Score).ToList();
        var scoreChange = orderedScores.Count < 2 ? 0 : orderedScores[^1] - orderedScores[0];

        var projects = await BuildProjectsAsync(cancellationToken);
        var gates = await _pullRequests.ListPullRequestsAsync(cancellationToken);

        return new AnalyticsOverviewDto
        {
            Days = days,
            ScanCount = scans.Count,
            AverageScore = scans.Count == 0 ? 0 : Math.Round(scans.Average(item => item.Score), 1),
            ScoreChange = scoreChange,
            OpenCritical = openIssues.Count(item => IsSeverity(item.Severity, "Critical")),
            OpenHigh = openIssues.Count(item => IsSeverity(item.Severity, "High")),
            ResolvedIssues = issues.Count(item => string.Equals(item.Status, "Resolved", StringComparison.OrdinalIgnoreCase)),
            Points = points,
            Projects = projects,
            RecentGates = gates.Take(8).ToList()
        };
    }

    private async Task<IReadOnlyList<ProjectRiskDto>> BuildProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await _db.ScanProjects
            .AsNoTracking()
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new
            {
                item.Id,
                item.FileName,
                item.DisplayName,
                item.RepositoryUrl,
                item.Status,
                Latest = item.Results
                    .OrderByDescending(result => result.CreatedAt)
                    .Select(result => new { result.Id, result.Score, result.SecurityScore, result.Status })
                    .FirstOrDefault(),
                PreviousScore = item.Results
                    .OrderByDescending(result => result.CreatedAt)
                    .Skip(1)
                    .Select(result => (int?)result.Score)
                    .FirstOrDefault()
            })
            .Take(12)
            .ToListAsync(cancellationToken);

        var latestIds = projects.Where(item => item.Latest != null).Select(item => item.Latest!.Id).ToList();
        var issueRows = latestIds.Count == 0
            ? []
            : await _db.ScanIssues.AsNoTracking()
                .Where(item => latestIds.Contains(item.ScanResultId))
                .Select(item => new IssueFact(item.ScanResultId, item.Severity, item.Status))
                .ToListAsync(cancellationToken);

        return projects.Select(project =>
        {
            var open = issueRows.Where(item => project.Latest != null && item.ScanResultId == project.Latest.Id && IsOpen(item.Status)).ToList();
            var trend = "Stable";
            if (project.Latest != null && project.PreviousScore.HasValue)
            {
                if (project.Latest.Score > project.PreviousScore.Value) trend = "Improving";
                else if (project.Latest.Score < project.PreviousScore.Value) trend = "Regressing";
            }

            return new ProjectRiskDto
            {
                ProjectId = project.Id,
                Name = string.IsNullOrWhiteSpace(project.DisplayName) ? project.FileName : project.DisplayName,
                RepositoryUrl = project.RepositoryUrl,
                LatestScore = project.Latest?.Score ?? 0,
                SecurityScore = project.Latest?.SecurityScore ?? 0,
                OpenCritical = open.Count(item => IsSeverity(item.Severity, "Critical")),
                OpenHigh = open.Count(item => IsSeverity(item.Severity, "High")),
                Trend = trend,
                LatestScanResultId = project.Latest?.Id,
                Status = project.Latest?.Status ?? project.Status
            };
        }).ToList();
    }

    private static bool IsOpen(string? status) =>
        !string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, "AcceptedRisk", StringComparison.OrdinalIgnoreCase);

    private static bool IsSeverity(string severity, string expected) =>
        string.Equals(severity, expected, StringComparison.OrdinalIgnoreCase);

    private sealed record ScanFact(int Id, Guid ScanProjectId, DateTime CreatedAt, int Score, int SecurityScore, string Status, int? PreviousScore);

    private sealed record IssueFact(int ScanResultId, string Severity, string? Status);
}
