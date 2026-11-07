using System.Security.Cryptography;
using System.Text;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.GitHub;

public static class GitHubWebhookVerifier
{
    public static bool IsValid(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHeader[prefix.Length..].Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return CryptographicOperations.FixedTimeEquals(hash, provided);
    }
}

public static class GitHubPolicyEvaluator
{
    public static PolicyDecision Evaluate(RepositoryRegistration policy, ScanComparison comparison, int score, int? previousScore)
    {
        var actionable = comparison.Items.Where(item =>
            !policy.AllowAcceptedRisk || !item.Status.Equals("AcceptedRisk", StringComparison.OrdinalIgnoreCase)).ToList();

        bool IsNew(ComparisonItem item) =>
            item.Change is "New" or "Recurring" ||
            (comparison.PreviousScanResultId == null && item.Change == "Detected");

        var newCritical = actionable.Count(item => IsNew(item) && item.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase));
        var newHigh = actionable.Count(item => IsNew(item) && item.Severity.Equals("High", StringComparison.OrdinalIgnoreCase));
        var warnings = new List<string>();
        var failed = false;

        if (policy.FailOnNewCritical && newCritical > 0)
        {
            failed = true;
            warnings.Add($"{newCritical} new or recurring critical finding(s).");
        }

        if (policy.WarnOnHigh && newHigh > 0)
            warnings.Add($"{newHigh} new or recurring high finding(s).");

        if (policy.PreventScoreRegression && previousScore.HasValue && score < previousScore.Value)
        {
            failed = true;
            warnings.Add($"Security score regressed from {previousScore} to {score}.");
        }

        if (warnings.Count == 0)
            warnings.Add("No policy threshold was crossed.");

        return new PolicyDecision(failed, newCritical, newHigh, warnings);
    }
}

public sealed record PolicyDecision(bool Failed, int NewCritical, int NewHigh, IReadOnlyList<string> Messages);

public static class GitHubWorkflowTemplate
{
    public const string FileName = "prodfalcon.yml";

    public static string Create(string apiBaseUrl) => $$$"""
        name: ProdFalcon
        on:
          pull_request:
        jobs:
          scan:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4
              - name: Package the repository
                run: zip -r prodfalcon.zip . -x "*.git*"
              - name: Scan and enforce policy
                env:
                  PRODFALCON_TOKEN: ${{ secrets.PRODFALCON_TOKEN }}
                run: |
                  curl --fail-with-body -sS -X POST \
                    -H "Authorization: Bearer $PRODFALCON_TOKEN" \
                    -F "file=@prodfalcon.zip" \
                    -F "branch=${{ github.ref_name }}" \
                    -F "commitSha=${{ github.sha }}" \
                    "{{{apiBaseUrl}}}/api/github/ci"
        """;
}
