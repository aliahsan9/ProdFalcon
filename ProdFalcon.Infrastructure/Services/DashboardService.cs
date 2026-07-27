using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(ApplicationDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building dashboard summary");

        var results = await _db.ScanResults
            .Include(r => r.Issues)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var issues = results.SelectMany(r => r.Issues).ToList();

        static double AvgOrZero(IReadOnlyList<Application.Scanning.Models.ScanResult> list, Func<Application.Scanning.Models.ScanResult, int> selector) =>
            list.Count == 0 ? 0 : Math.Round(list.Average(r => selector(r)), 1);

        var avgScore = AvgOrZero(results, r => r.Score);
        var avgSecurity = AvgOrZero(results, r => r.SecurityScore);
        var avgMaintainability = AvgOrZero(results, r => r.MaintainabilityScore);
        var avgPerformance = AvgOrZero(results, r => r.PerformanceScore);
        var avgProduction = AvgOrZero(results, r => r.ProductionReadinessScore);

        var criticalHigh = issues.Count(i =>
            string.Equals(i.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Severity, "High", StringComparison.OrdinalIgnoreCase));

        var smellDensity = results.Count == 0
            ? 0
            : Math.Min(100, issues.Count * 4.0 / Math.Max(1, results.Count));

        var architecture = Math.Round((avgMaintainability + avgPerformance) / 2.0, 1);
        var documentation = Math.Round(Clamp(avgMaintainability - (issues.Count(i =>
            i.Category.Contains("doc", StringComparison.OrdinalIgnoreCase)) * 2)), 1);
        var testCoverage = Math.Round(Clamp(avgMaintainability - 8 + (avgScore / 20)), 1);
        var technicalDebt = Math.Round(Clamp(100 - smellDensity - (criticalHigh * 1.5)), 1);
        var codeSmells = Math.Round(Clamp(100 - smellDensity), 1);
        var complexity = Math.Round(Clamp((avgPerformance + avgMaintainability) / 2.0), 1);
        var bugRisk = Math.Round(Clamp(100 - avgSecurity - (criticalHigh * 0.8)), 1);

        var recommendations = BuildRecommendations(
            avgSecurity, avgMaintainability, avgPerformance, avgProduction, criticalHigh, issues.Count);

        var summary = new DashboardSummaryDto
        {
            TotalProjects = await _db.ScanProjects.CountAsync(cancellationToken),
            TotalScans = results.Count,
            TotalIssues = issues.Count,
            AverageScore = avgScore,
            AverageSecurityScore = avgSecurity,
            AverageMaintainabilityScore = avgMaintainability,
            AveragePerformanceScore = avgPerformance,
            AverageProductionReadinessScore = avgProduction,
            ArchitectureScore = architecture,
            DocumentationScore = documentation,
            TestCoverageScore = testCoverage,
            TechnicalDebtScore = technicalDebt,
            CodeSmellsScore = codeSmells,
            ComplexityScore = complexity,
            BugRiskScore = bugRisk,
            IssuesBySeverity = issues
                .GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            IssuesByCategory = issues
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopViolatedRules = issues
                .GroupBy(i => i.RuleName)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            AiRecommendations = recommendations
        };

        _logger.LogInformation(
            "Dashboard summary ready: {Projects} projects, {Scans} scans, {Issues} issues, avg {Score}",
            summary.TotalProjects, summary.TotalScans, summary.TotalIssues, summary.AverageScore);

        return summary;
    }

    public async Task<IReadOnlyList<ProjectDashboardDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading dashboard projects");

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
                LatestSecurityScore = latest?.SecurityScore ?? 0,
                LatestMaintainabilityScore = latest?.MaintainabilityScore ?? 0,
                LatestPerformanceScore = latest?.PerformanceScore ?? 0,
                LatestProductionReadinessScore = latest?.ProductionReadinessScore ?? 0,
                LatestScanResultId = latest?.Id ?? 0,
                ScanCount = p.Results.Count,
                IssueCount = p.Results.SelectMany(r => r.Issues).Count(),
                Trend = trend
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ScanTrendDto>> GetTrendsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading scan trends");

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

    private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));

    private static IReadOnlyList<string> BuildRecommendations(
        double security,
        double maintainability,
        double performance,
        double production,
        int criticalHigh,
        int totalIssues)
    {
        var list = new List<string>();

        if (security < 70)
            list.Add("Prioritize a Security Scan — secrets, auth gaps, and OWASP risks are dragging your score.");
        if (criticalHigh > 0)
            list.Add($"Remediate {criticalHigh} critical/high findings before the next release.");
        if (maintainability < 70)
            list.Add("Reduce technical debt: split large modules and address maintainability findings.");
        if (performance < 70)
            list.Add("Profile slow paths and caching opportunities flagged in Performance Scan.");
        if (production < 75)
            list.Add("Tighten production readiness — logging, CORS, and debug flags need attention.");
        if (totalIssues == 0)
            list.Add("No issues found yet. Upload a project ZIP or connect a repository to start scanning.");
        if (list.Count == 0)
            list.Add("Health looks solid. Enable CI/CD scans on every PR to keep the streak going.");

        return list.Take(5).ToList();
    }
}
