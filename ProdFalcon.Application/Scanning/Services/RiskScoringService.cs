using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Services;

public class RiskScoringService : IRiskScoringService
{
    private static readonly HashSet<string> SecurityRules =
    [
        nameof(Rules.ApiKeyExposureRule),
        nameof(Rules.HardcodedConnectionStringRule),
        nameof(Rules.HardcodedJwtSecretRule),
        nameof(Rules.SqlInjectionRiskRule),
        nameof(Rules.PlainTextPasswordRule),
        nameof(Rules.CorsWildcardRule),
        nameof(Rules.MissingAuthorizationRule)
    ];

    private static readonly HashSet<string> PerformanceRules =
    [
        nameof(Rules.HttpUsageRule),
        nameof(Rules.DebugModeRule)
    ];

    private static readonly HashSet<string> MaintainabilityRules =
    [
        nameof(Rules.MissingLoggingRule),
        nameof(Rules.SensitiveLoggingRule)
    ];

    private static readonly HashSet<string> ProductionRules =
    [
        nameof(Rules.SwaggerInProductionRule),
        nameof(Rules.DebugModeRule)
    ];

    public RiskScoreResult Calculate(IEnumerable<ScanIssue> issues)
    {
        var issueList = issues.ToList();

        return new RiskScoreResult
        {
            SecurityScore = CalculateCategoryScore(issueList, SecurityRules),
            PerformanceScore = CalculateCategoryScore(issueList, PerformanceRules),
            MaintainabilityScore = CalculateCategoryScore(issueList, MaintainabilityRules),
            ProductionReadinessScore = CalculateCategoryScore(issueList, ProductionRules),
            OverallScore = CalculateOverall(issueList)
        };
    }

    private static int CalculateOverall(List<ScanIssue> issues)
    {
        const int baseScore = 100;
        var deduction = issues.Sum(i => SeverityWeight(i.Severity));
        return Math.Clamp(baseScore - deduction, 0, 100);
    }

    private static int CalculateCategoryScore(List<ScanIssue> issues, HashSet<string> ruleNames)
    {
        const int baseScore = 100;
        var relevant = issues.Where(i =>
            ruleNames.Contains(i.RuleName) ||
            ruleNames.Any(r => i.RuleId.Contains(r, StringComparison.OrdinalIgnoreCase)));

        var deduction = relevant.Sum(i => SeverityWeight(i.Severity));
        return Math.Clamp(baseScore - deduction, 0, 100);
    }

    private static int SeverityWeight(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "critical" => 25,
            "high" => 15,
            "medium" => 10,
            "low" => 5,
            _ => 5
        };
}
