using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Remediation;

public sealed class RemediationDraft
{
    public string Explanation { get; init; } = string.Empty;

    public string SecureSuggestion { get; init; } = string.Empty;

    public string OriginalSnippet { get; init; } = string.Empty;

    public string PatchedSnippet { get; init; } = string.Empty;

    public string DiffText { get; init; } = string.Empty;

    public string Grounding { get; init; } = "Stored finding. Reachability was not proven.";
}

public sealed class RemediationProposalDto
{
    public int Id { get; init; }

    public int ScanResultId { get; init; }

    public int ScanIssueId { get; init; }

    public string RuleId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int LineNumber { get; init; }

    public int ColumnNumber { get; init; }

    public string Explanation { get; init; } = string.Empty;

    public string SecureSuggestion { get; init; } = string.Empty;

    public string OriginalSnippet { get; init; } = string.Empty;

    public string PatchedSnippet { get; init; } = string.Empty;

    public string DiffText { get; init; } = string.Empty;

    public string Status { get; init; } = "Proposed";

    public bool UsedExternalModel { get; init; }

    public string Grounding { get; init; } = string.Empty;

    public string? DecisionNote { get; init; }
}

public sealed class RemediationPlanDto
{
    public int ScanResultId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<RemediationProposalDto> Proposals { get; init; } = [];
}

/// <summary>
/// Builds an explanation and a reviewable snippet patch from a finding the scanner already stored.
/// It does not invent a second vulnerability and it does not rewrite the repository.
/// </summary>
public static class RemediationComposer
{
    public static RemediationDraft Draft(ScanIssue issue) =>
        Draft(
            issue.Title,
            issue.FilePath,
            issue.LineNumber,
            issue.ColumnNumber,
            issue.Cwe,
            issue.Description,
            issue.Impact,
            issue.Remediation,
            issue.SecureExample,
            issue.Evidence,
            issue.DetectionMethod);

    public static RemediationDraft Draft(ScanIssueSummaryDto issue) =>
        Draft(
            issue.Title,
            issue.FilePath,
            issue.LineNumber,
            issue.ColumnNumber,
            issue.Cwe,
            issue.Description,
            issue.Impact,
            issue.Remediation,
            issue.SecureExample,
            issue.Evidence,
            issue.DetectionMethod);

    public static RemediationDraft Draft(
        string? title,
        string? filePath,
        int lineNumber,
        int columnNumber,
        string? cwe,
        string? description,
        string? impact,
        string? remediation,
        string? secureExample,
        string? evidence,
        string? detectionMethod)
    {
        var location = Location(filePath, lineNumber, columnNumber);
        var original = OriginalSnippet(evidence, title, lineNumber);
        var patched = string.IsNullOrWhiteSpace(secureExample) ? FirstLine(remediation) : secureExample.Trim();
        if (string.IsNullOrWhiteSpace(patched))
            patched = "Review the finding and replace the cited line with a parameterized or configured equivalent.";

        var suggestion = string.IsNullOrWhiteSpace(remediation)
            ? patched
            : remediation.Trim();

        var explanation =
            $"{title} at {location}" +
            (string.IsNullOrWhiteSpace(cwe) ? ". " : $" ({cwe}). ") +
            Trim(description, 500) + " " +
            Trim(impact, 400) + " " +
            "Detected by " + (string.IsNullOrWhiteSpace(detectionMethod) ? "static analysis" : detectionMethod.Trim()) +
            ". This explanation uses only the stored finding. It does not prove the issue is exploitable.";

        return new RemediationDraft
        {
            Explanation = explanation.Trim(),
            SecureSuggestion = suggestion,
            OriginalSnippet = original,
            PatchedSnippet = patched,
            DiffText = BuildDiff(filePath, lineNumber, original, patched),
            Grounding = "Stored finding and the rule's secure example. Approving records a decision and does not modify the repository."
        };
    }

    public static string Location(string? filePath, int lineNumber, int columnNumber)
    {
        var name = string.IsNullOrWhiteSpace(filePath) ? "source" : filePath.Replace('\\', '/');
        if (columnNumber > 0)
            return $"{name}:{Math.Max(1, lineNumber)}:{columnNumber}";
        return $"{name}:{Math.Max(1, lineNumber)}";
    }

    private static string BuildDiff(string? filePath, int lineNumber, string original, string patched)
    {
        var name = string.IsNullOrWhiteSpace(filePath) ? "source" : Path.GetFileName(filePath.Replace('\\', '/'));
        var line = Math.Max(1, lineNumber);
        var builder = new System.Text.StringBuilder();
        builder.Append("--- a/").AppendLine(name);
        builder.Append("+++ b/").AppendLine(name);
        builder.Append("@@ -").Append(line).Append(",1 +").Append(line).AppendLine(",1 @@");
        builder.Append("- ").AppendLine(OneLine(original));
        foreach (var row in patched.Replace("\r", string.Empty).Split('\n'))
            builder.Append("+ ").AppendLine(OneLine(row));
        builder.AppendLine("# Suggested snippet. The repository was not modified.");
        return builder.ToString().TrimEnd();
    }

    private static string OriginalSnippet(string? evidence, string? title, int lineNumber)
    {
        var text = evidence ?? string.Empty;
        var pipe = text.IndexOf('|');
        if (pipe >= 0)
            text = text[(pipe + 1)..];

        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            text = $"line {Math.Max(1, lineNumber)}: {title}";

        return OneLine(text);
    }

    private static string FirstLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var line = value.Replace("\r", string.Empty).Split('\n')[0];
        return line.Trim();
    }

    private static string OneLine(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 240 ? text : text[..240] + "...";
    }

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        return text.Length <= max ? text : text[..max] + "...";
    }
}
