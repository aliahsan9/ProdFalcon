using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Remediation;

public static class FindingStatuses
{
    public const string Detected = "Detected";
    public const string Acknowledged = "Acknowledged";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Verified = "Verified";
    public const string AcceptedRisk = "AcceptedRisk";

    public static readonly string[] Manual =
    [
        Detected, Acknowledged, InProgress, Resolved, AcceptedRisk
    ];

    public static bool IsOpen(string? status) =>
        status is null or "" or "Open" or Detected or Acknowledged or InProgress;

    public static bool CountsAgainstScore(string? status) => IsOpen(status);
}

public static class FindingReconciler
{
    public static ScanComparison Reconcile(
        IList<TrackedFinding> tracked,
        IReadOnlyList<ScanIssue> current,
        int previousScanResultId,
        string? previousBranch,
        string? currentBranch,
        string? previousCommit,
        string? currentCommit,
        DateTime now)
    {
        var comparison = new ScanComparison
        {
            PreviousScanResultId = previousScanResultId == 0 ? null : previousScanResultId,
            PreviousBranch = previousBranch,
            CurrentBranch = currentBranch,
            PreviousCommit = previousCommit,
            CurrentCommit = currentCommit
        };

        var currentByFingerprint = current
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Fingerprint))
            .GroupBy(issue => issue.Fingerprint, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in tracked)
        {
            if (!currentByFingerprint.TryGetValue(finding.Fingerprint, out var issue))
            {
                if (finding.Status == FindingStatuses.Resolved)
                {
                    finding.Status = FindingStatuses.Verified;
                    finding.VerifiedAt = now;
                    comparison.VerifiedCount++;
                    comparison.Items.Add(Item(finding, "Verified"));
                }
                else if (finding.Status is FindingStatuses.Verified or FindingStatuses.AcceptedRisk)
                {
                    comparison.NotObservedCount++;
                    comparison.Items.Add(Item(finding, "NotObserved"));
                }
                else if (FindingStatuses.IsOpen(finding.Status))
                {
                    comparison.NotObservedCount++;
                    comparison.Items.Add(Item(finding, "NotObserved"));
                }

                continue;
            }

            seen.Add(finding.Fingerprint);
            finding.LastSeenAt = now;
            finding.OccurrenceCount++;
            finding.Severity = issue.Severity;
            finding.FilePath = issue.FilePath;
            finding.Title = issue.Title;

            if (finding.Status == FindingStatuses.Verified || finding.Status == FindingStatuses.Resolved)
            {
                finding.Status = FindingStatuses.Detected;
                finding.VerifiedAt = null;
                finding.ResolvedAt = null;
                comparison.RecurringCount++;
                comparison.Items.Add(Item(finding, "Recurring"));
                issue.Status = FindingStatuses.Detected;
            }
            else if (finding.Status == FindingStatuses.AcceptedRisk)
            {
                comparison.AcceptedRiskCount++;
                comparison.Items.Add(Item(finding, "AcceptedRisk"));
                issue.Status = FindingStatuses.AcceptedRisk;
            }
            else
            {
                comparison.UnchangedCount++;
                comparison.Items.Add(Item(finding, "Unchanged"));
                issue.Status = string.IsNullOrWhiteSpace(finding.Status) ? FindingStatuses.Detected : finding.Status;
            }
        }

        foreach (var issue in current)
        {
            if (string.IsNullOrWhiteSpace(issue.Fingerprint) || !seen.Add(issue.Fingerprint))
                continue;

            if (tracked.Any(item => item.Fingerprint.Equals(issue.Fingerprint, StringComparison.OrdinalIgnoreCase)))
                continue;

            tracked.Add(new TrackedFinding
            {
                Fingerprint = issue.Fingerprint,
                RuleId = issue.RuleId,
                Title = issue.Title,
                FilePath = issue.FilePath,
                Severity = issue.Severity,
                Status = FindingStatuses.Detected,
                OccurrenceCount = 1,
                FirstSeenAt = now,
                LastSeenAt = now
            });
            issue.Status = FindingStatuses.Detected;
            comparison.NewCount++;
            comparison.Items.Add(new ComparisonItem
            {
                Fingerprint = issue.Fingerprint,
                Title = issue.Title,
                Severity = issue.Severity,
                FilePath = issue.FilePath,
                Change = previousScanResultId == 0 ? "Detected" : "New",
                Status = FindingStatuses.Detected
            });
        }

        if (previousScanResultId == 0)
        {
            comparison.NewCount = 0;
            foreach (var item in comparison.Items.Where(item => item.Change == "New"))
                item.Change = "Detected";
        }

        return comparison;
    }

    private static ComparisonItem Item(TrackedFinding finding, string change) => new()
    {
        Fingerprint = finding.Fingerprint,
        Title = finding.Title,
        Severity = finding.Severity,
        FilePath = finding.FilePath,
        Change = change,
        Status = finding.Status
    };
}
