using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var results = await _db.ScanResults
            .Include(r => r.Issues)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var issues = results.SelectMany(r => r.Issues).ToList();

        return new DashboardSummaryDto
        {
            TotalProjects = await _db.ScanProjects.CountAsync(cancellationToken),
            TotalScans = results.Count,
            TotalIssues = issues.Count,
            AverageScore = results.Count == 0 ? 0 : Math.Round(results.Average(r => r.Score), 1),
            IssuesBySeverity = issues
                .GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopViolatedRules = issues
                .GroupBy(i => i.RuleName)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<IReadOnlyList<ProjectDashboardDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _db.ScanProjects
            .Include(p => p.Results)
            .ThenInclude(r => r.Issues)
            .AsNoTracking()
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken);

        return projects.Select(p =>
        {
            var ordered = p.Results.OrderByDescending(r => r.CreatedAt).ToList();
            var latest = ordered.FirstOrDefault();
            var previous = ordered.Skip(1).FirstOrDefault();

            var trend = "Stable";
            if (latest != null && previous != null)
            {
                if (latest.Score > previous.Score) trend = "Improving";
                else if (latest.Score < previous.Score) trend = "Degrading";
            }

            return new ProjectDashboardDto
            {
                ProjectId = p.Id,
                FileName = p.FileName,
                UploadedAt = p.UploadedAt,
                Status = p.Status,
                LatestScore = latest?.Score ?? 0,
                LatestScanResultId = latest?.Id ?? 0,
                ScanCount = p.Results.Count,
                IssueCount = p.Results.SelectMany(r => r.Issues).Count(),
                Trend = trend
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ScanTrendDto>> GetTrendsAsync(CancellationToken cancellationToken = default)
    {
        var results = await _db.ScanResults
            .Include(r => r.Issues)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new ScanTrendDto
            {
                Date = g.Key,
                AverageScore = Math.Round(g.Average(r => r.Score), 1),
                ScanCount = g.Count(),
                IssueCount = g.Sum(r => r.Issues.Count)
            })
            .OrderBy(t => t.Date)
            .ToList();
    }
}
