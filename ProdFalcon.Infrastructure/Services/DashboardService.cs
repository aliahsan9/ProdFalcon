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
        _db.Database.SetCommandTimeout(30);

        var scores = await _db.ScanResults
            .AsNoTracking()
            .Select(r => new
            {
                r.Score,
                r.SecurityScore,
                r.MaintainabilityScore,
                r.PerformanceScore,
                r.ProductionReadinessScore
            })
            .ToListAsync(cancellationToken);

        var issues = await LoadIssueFactsAsync(cancellationToken);

        static double AvgOrZero(IReadOnlyList<int> values) =>
            values.Count == 0 ? 0 : Math.Round(values.Average(), 1);

        var avgScore = AvgOrZero(scores.Select(r => r.Score).ToList());
        var avgSecurity = AvgOrZero(scores.Select(r => r.SecurityScore).ToList());
        var avgMaintainability = AvgOrZero(scores.Select(r => r.MaintainabilityScore).ToList());
        var avgPerformance = AvgOrZero(scores.Select(r => r.PerformanceScore).ToList());
        var avgProduction = AvgOrZero(scores.Select(r => r.ProductionReadinessScore).ToList());

        var criticalHigh = issues.Count(i =>
            string.Equals(i.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Severity, "High", StringComparison.OrdinalIgnoreCase));

        var scoring = new Application.Scanning.Services.RiskScoringService();
        var architecture = scoring.Calculate(issues.Where(i => i.Category.Equals("Architecture", StringComparison.OrdinalIgnoreCase))).OverallScore;
        var complexity = scoring.Calculate(issues.Where(i => i.RuleId.Contains("COMPLEX", StringComparison.OrdinalIgnoreCase))).OverallScore;
        var technicalDebt = avgMaintainability;
        var codeSmells = avgMaintainability;

        var recommendations = BuildRecommendations(
            avgSecurity, avgMaintainability, avgPerformance, avgProduction, criticalHigh, issues.Count);

        var summary = new DashboardSummaryDto
        {
            TotalProjects = await _db.ScanProjects.CountAsync(cancellationToken),
            TotalScans = scores.Count,
            TotalIssues = issues.Count,
            AverageScore = avgScore,
            AverageSecurityScore = avgSecurity,
            AverageMaintainabilityScore = avgMaintainability,
            AveragePerformanceScore = avgPerformance,
            AverageProductionReadinessScore = avgProduction,
            ArchitectureScore = architecture,
            DocumentationScore = -1,
            TestCoverageScore = -1,
            TechnicalDebtScore = technicalDebt,
            CodeSmellsScore = codeSmells,
            ComplexityScore = complexity,
            BugRiskScore = -1,
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
            AiRecommendations = recommendations,
            CriticalVulnerabilities = CountOpen(issues, "Critical"),
            HighVulnerabilities = CountOpen(issues, "High"),
            MediumVulnerabilities = CountOpen(issues, "Medium"),
            LowVulnerabilities = CountOpen(issues, "Low"),
            InformationalFindings = issues.Count(i => IsOpen(i) && (IsSeverity(i, "Info") || IsSeverity(i, "Informational"))),
            ResolvedIssues = issues.Count(i => string.Equals(i.Status, "Resolved", StringComparison.OrdinalIgnoreCase)),
            OpenIssues = issues.Count(IsOpen)
        };

        _logger.LogInformation(
            "Dashboard summary ready: {Projects} projects, {Scans} scans, {Issues} issues, avg {Score}",
            summary.TotalProjects, summary.TotalScans, summary.TotalIssues, summary.AverageScore);

        return summary;
    }

    public async Task<IReadOnlyList<ProjectDashboardDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading dashboard projects");
        _db.Database.SetCommandTimeout(30);

        var projects = await _db.ScanProjects
            .AsNoTracking()
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.UploadedAt,
                p.Status,
                p.SourceType,
                p.RepositoryUrl,
                p.Branch,
                p.DisplayName,
                ScanCount = p.Results.Count,
                IssueCount = p.Results
                    .SelectMany(r => r.Issues)
                    .Count(i => i.Status == null || i.Status.ToLower() != "resolved"),
                LatestId = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.Id).FirstOrDefault(),
                LatestScore = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.Score).FirstOrDefault(),
                LatestSecurityScore = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.SecurityScore).FirstOrDefault(),
                LatestMaintainabilityScore = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.MaintainabilityScore).FirstOrDefault(),
                LatestPerformanceScore = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.PerformanceScore).FirstOrDefault(),
                LatestProductionReadinessScore = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.ProductionReadinessScore).FirstOrDefault(),
                LatestDurationMs = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => (int?)r.DurationMs).FirstOrDefault(),
                LatestStatus = p.Results.OrderByDescending(r => r.CreatedAt).Select(r => r.Status).FirstOrDefault(),
                PreviousScore = p.Results.OrderByDescending(r => r.CreatedAt).Skip(1).Select(r => (int?)r.Score).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return projects.Select(p =>
        {
            var trend = "Stable";
            if (p.LatestScore.HasValue && p.PreviousScore.HasValue)
            {
                if (p.LatestScore.Value > p.PreviousScore.Value) trend = "Improving";
                else if (p.LatestScore.Value < p.PreviousScore.Value) trend = "Degrading";
            }

            return new ProjectDashboardDto
            {
                ProjectId = p.Id,
                FileName = p.FileName,
                UploadedAt = p.UploadedAt,
                Status = p.Status,
                LatestScore = p.LatestScore ?? 0,
                LatestSecurityScore = p.LatestSecurityScore ?? 0,
                LatestMaintainabilityScore = p.LatestMaintainabilityScore ?? 0,
                LatestPerformanceScore = p.LatestPerformanceScore ?? 0,
                LatestProductionReadinessScore = p.LatestProductionReadinessScore ?? 0,
                LatestScanResultId = p.LatestId ?? 0,
                ScanCount = p.ScanCount,
                IssueCount = p.IssueCount,
                Trend = trend,
                SourceType = p.SourceType,
                RepositoryUrl = p.RepositoryUrl,
                Branch = p.Branch,
                DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.FileName : p.DisplayName,
                LatestDurationMs = p.LatestDurationMs ?? 0,
                LatestStatus = p.LatestStatus ?? p.Status
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ScanTrendDto>> GetTrendsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading scan trends");
        _db.Database.SetCommandTimeout(30);

        var results = await _db.ScanResults
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.CreatedAt,
                r.Score,
                IssueCount = r.Issues.Count
            })
            .ToListAsync(cancellationToken);

        return results
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new ScanTrendDto
            {
                Date = g.Key,
                AverageScore = Math.Round(g.Average(r => r.Score), 1),
                ScanCount = g.Count(),
                IssueCount = g.Sum(r => r.IssueCount)
            })
            .OrderBy(t => t.Date)
            .ToList();
    }

    /// <summary>
    /// Dashboard math only needs severity, category, and status. Loading full issue rows
    /// joins them to ScanResult and repeats AnalysisDocument once per finding.
    /// </summary>
    private async Task<List<Application.Scanning.Models.ScanIssue>> LoadIssueFactsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.ScanIssues
            .AsNoTracking()
            .Select(i => new
            {
                i.Severity,
                i.Category,
                i.RuleName,
                i.RuleId,
                i.Status,
                i.Confidence
            })
            .ToListAsync(cancellationToken);

        return rows.Select(i => new Application.Scanning.Models.ScanIssue
        {
            Severity = i.Severity ?? "Info",
            Category = string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category,
            RuleName = i.RuleName ?? string.Empty,
            RuleId = i.RuleId ?? string.Empty,
            Status = i.Status ?? "Open",
            Confidence = string.IsNullOrWhiteSpace(i.Confidence) ? "High" : i.Confidence
        }).ToList();
    }

    private static bool IsOpen(Application.Scanning.Models.ScanIssue issue) =>
        !string.Equals(issue.Status, "Resolved", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(issue.Status, "AcceptedRisk", StringComparison.OrdinalIgnoreCase);

    private static bool IsSeverity(Application.Scanning.Models.ScanIssue issue, string severity) =>
        string.Equals(issue.Severity, severity, StringComparison.OrdinalIgnoreCase);

    private static int CountOpen(List<Application.Scanning.Models.ScanIssue> issues, string severity) =>
        issues.Count(i => IsOpen(i) && IsSeverity(i, severity));

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
