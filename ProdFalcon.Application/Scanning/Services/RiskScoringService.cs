using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Services;

/// <summary>
/// Indicative score. See <see cref="SecurityScoreMethodology"/> for the published explanation.
/// </summary>
public class RiskScoringService : IRiskScoringService
{
    public RiskScoreResult Calculate(IEnumerable<ScanIssue> issues)
    {
        var open = issues.Where(IsOpen).ToList();
        var result = new RiskScoreResult
        {
            SecurityScore = Score(open.Where(IsSecurity)),
            PerformanceScore = Score(open.Where(i => IsCategory(i, "Performance"))),
            MaintainabilityScore = Score(open.Where(i => IsCategory(i, "Maintainability", "Quality"))),
            ProductionReadinessScore = Score(open.Where(i => IsCategory(i, "Production", "Configuration"))),
            OverallScore = Score(open)
        };

        result.RiskLevel = ResolveLevel(result.OverallScore, open);
        return result;
    }

    private static int Score(IEnumerable<ScanIssue> issues)
    {
        var deduction = 0d;
        foreach (var issue in issues)
            deduction += SeverityWeight(issue.Severity) * ConfidenceFactor(issue.Confidence);

        return (int)Math.Clamp(Math.Round(100 - deduction), 0, 100);
    }

    private static string ResolveLevel(int score, List<ScanIssue> open)
    {
        if (open.Any(i => IsSeverity(i, "Critical")))
            return "Critical";
        if (score < 60 || open.Any(i => IsSeverity(i, "High")))
            return "High";
        if (score < 80 || open.Any(i => IsSeverity(i, "Medium")))
            return "Medium";
        return "Low";
    }

    internal static bool IsOpen(ScanIssue issue)
    {
        var status = issue.Status ?? "Open";
        return !status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("Verified", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("AcceptedRisk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecurity(ScanIssue issue) =>
        IsCategory(issue, "Security", "Secrets", "Dependencies", "Authentication", "Injection", "Configuration", "RequestForgery", "ServerSideRequest");

    private static bool IsCategory(ScanIssue issue, params string[] categories) =>
        categories.Any(c => string.Equals(issue.Category, c, StringComparison.OrdinalIgnoreCase));

    private static bool IsSeverity(ScanIssue issue, string severity) =>
        string.Equals(issue.Severity, severity, StringComparison.OrdinalIgnoreCase);

    private static int SeverityWeight(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "critical" => 25,
            "high" => 15,
            "medium" => 8,
            "low" => 3,
            _ => 0
        };

    private static double ConfidenceFactor(string? confidence) =>
        (confidence ?? "High").ToLowerInvariant() switch
        {
            "low" => 0.5,
            "medium" => 0.75,
            _ => 1
        };
}
