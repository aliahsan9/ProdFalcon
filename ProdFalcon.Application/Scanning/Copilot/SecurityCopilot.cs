using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Copilot;

/// <summary>
/// Answers from stored scan evidence. It does not invent findings or claim tests that were not run.
/// </summary>
public static class SecurityCopilot
{
    public static string Answer(string question, ScanResult scan, ProjectAnalysisDocument? analysis)
    {
        var text = question ?? string.Empty;
        var open = scan.Issues.Where(IsOpen).ToList();

        if (Contains(text, "architecture", "organized", "structure"))
            return ArchitectureAnswer(analysis);

        if (Contains(text, "solid"))
            return SolidAnswer(open);

        if (Contains(text, "production"))
            return ProductionAnswer(scan, analysis);

        if (Contains(text, "dependenc", "upgrade", "nuget", "package"))
            return DependencyAnswer(open, analysis);

        if (Contains(text, "auth"))
            return AuthAnswer(open, analysis);

        if (Contains(text, "database", "n+1", "performance"))
            return PerformanceAnswer(open, analysis);

        if (Contains(text, "important", "first", "priorit", "five"))
            return PriorityAnswer(open);

        if (Contains(text, "compare", "previous"))
            return CompareAnswer(scan, analysis);

        return Summary(scan, open);
    }

    private static string ArchitectureAnswer(ProjectAnalysisDocument? analysis)
    {
        if (analysis == null)
            return "This scan has no project intelligence profile. Run a new scan to generate one from the repository.";

        var profile = analysis.Intelligence;
        var edges = analysis.Architecture.Edges;
        var uncertain = edges.Count(edge => edge.Kind.Equals("Uncertain", StringComparison.OrdinalIgnoreCase));
        return string.Join(" ", profile.Answers.Take(4)) +
               $" The architecture graph has {analysis.Architecture.Nodes.Count} components and {edges.Count} relationships, of which {uncertain} are uncertain. {analysis.Architecture.Note}";
    }

    private static string SolidAnswer(List<ScanIssue> open)
    {
        var findings = open.Where(issue =>
            issue.Category.Equals("Architecture", StringComparison.OrdinalIgnoreCase) ||
            issue.RuleId.StartsWith("QUAL-", StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
        if (findings.Count == 0)
            return "No architecture or structure finding in this scan supports a SOLID violation. Absence of a finding is not a design review.";

        return "These findings are the evidence for structural problems: " +
               string.Join(" ", findings.Select(issue => $"{issue.Title} at {issue.FilePath}:{issue.LineNumber}. {issue.Description}"));
    }

    private static string ProductionAnswer(ScanResult scan, ProjectAnalysisDocument? analysis)
    {
        var unverified = analysis?.UnverifiedAreas ?? ["Runtime behavior, coverage, and penetration testing were not measured."];
        return $"Indicative score {scan.Score}/100 (security {scan.SecurityScore}, maintainability {scan.MaintainabilityScore}, performance {scan.PerformanceScore}, production checks {scan.ProductionReadinessScore}). " +
               "This is not a release approval. Unverified areas: " + string.Join(" ", unverified);
    }

    private static string DependencyAnswer(List<ScanIssue> open, ProjectAnalysisDocument? analysis)
    {
        var vulnerable = open.Where(issue =>
            issue.Category.Contains("Depend", StringComparison.OrdinalIgnoreCase) ||
            issue.RuleId.StartsWith("DEP-", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(issue.Cve)).Take(8).ToList();
        if (vulnerable.Count == 0)
        {
            var names = analysis?.Intelligence.Dependencies.Take(8).Select(item => item.Name + (string.IsNullOrWhiteSpace(item.Version) ? "" : " " + item.Version)).ToList() ?? [];
            return names.Count == 0
                ? "No package manifest was parsed, and no dependency advisory finding is stored. Nothing was recommended for upgrade."
                : "No advisory finding is stored for these detected dependencies, so none are recommended for upgrade from this scan: " + string.Join(", ", names) + ".";
        }

        return "Upgrade or replace the dependencies named by these findings: " +
               string.Join(" ", vulnerable.Select(issue => $"{issue.Title} ({issue.Cve}) at {issue.FilePath}:{issue.LineNumber}."));
    }

    private static string AuthAnswer(List<ScanIssue> open, ProjectAnalysisDocument? analysis)
    {
        var findings = open.Where(issue => issue.Category.Contains("Auth", StringComparison.OrdinalIgnoreCase) || issue.RuleId.Contains("AUTH", StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
        var detected = analysis?.Intelligence.Authentication ?? [];
        var detectedText = detected.Count == 0
            ? "No authentication API was detected in the intelligence profile."
            : "Detected authentication evidence: " + string.Join("; ", detected.Take(4)) + ".";
        var findingText = findings.Count == 0
            ? " No open authorization finding is stored."
            : " Open findings: " + string.Join(" ", findings.Select(issue => $"{issue.Title} at {issue.FilePath}:{issue.LineNumber}."));
        return detectedText + findingText + " This does not prove the deployed system enforces authorization.";
    }

    private static string PerformanceAnswer(List<ScanIssue> open, ProjectAnalysisDocument? analysis)
    {
        var findings = open.Where(issue => issue.Category is "Performance" or "Reliability").Take(5).ToList();
        var note = analysis?.PerformanceNote ?? "These are static predictions.";
        if (findings.Count == 0)
            return "No performance or reliability finding is stored. " + note;
        return note + " " + string.Join(" ", findings.Select(issue => $"{issue.Title} at {issue.FilePath}:{issue.LineNumber}. {issue.Remediation}"));
    }

    private static string PriorityAnswer(List<ScanIssue> open)
    {
        var ranked = open.OrderByDescending(issue => Rank(issue.Severity)).Take(5).ToList();
        return ranked.Count == 0
            ? "This scan has no open findings to prioritize."
            : "The five highest open findings, by severity only, are: " +
              string.Join(" ", ranked.Select(issue => $"{issue.Severity} {issue.Title} at {issue.FilePath}:{issue.LineNumber}."));
    }

    private static string CompareAnswer(ScanResult scan, ProjectAnalysisDocument? analysis)
    {
        if (analysis?.Comparison.PreviousScanResultId == null && !scan.PreviousScore.HasValue)
            return "There is no earlier completed scan to compare.";

        var comparison = analysis?.Comparison;
        var score = scan.PreviousScore.HasValue
            ? $"Score moved from {scan.PreviousScore}/100 to {scan.Score}/100."
            : $"Current score is {scan.Score}/100.";
        if (comparison == null)
            return score + " Finding-level comparison was not stored for this scan.";

        return $"{score} New fingerprints: {comparison.NewCount}. Recurring: {comparison.RecurringCount}. Verified after a resolved finding disappeared: {comparison.VerifiedCount}. Still open and unchanged: {comparison.UnchangedCount}. Not observed, and not verified: {comparison.NotObservedCount}.";
    }

    private static string Summary(ScanResult scan, List<ScanIssue> open) =>
        open.Count == 0
            ? $"Scan {scan.Id} scored {scan.Score}/100 and has no open findings. That is not proof the project is secure."
            : $"Scan {scan.Id} scored {scan.Score}/100 with {open.Count} open findings. Highest severity: {open.OrderByDescending(issue => Rank(issue.Severity)).First().Severity}.";

    private static bool IsOpen(ScanIssue issue) =>
        !string.Equals(issue.Status, "Resolved", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(issue.Status, "AcceptedRisk", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(issue.Status, "Verified", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static int Rank(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "critical" => 5,
            "high" => 4,
            "medium" => 3,
            "low" => 2,
            _ => 1
        };
}
