using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class SensitiveLoggingRule : IScanRule, IScanModule
{
    private static readonly Regex LogCall = new(
        @"\bLog(Information|Warning|Error|Debug|Trace|Critical)?\s*\(|\b(logger|log)\.(Info|Warn|Error|Debug|Trace|Critical|Log)\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Sensitive = new(
        @"\b(password|passwd|pwd|secret|api[_-]?key|creditcard|ssn)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string ModuleKey => ScanModules.Quality;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (issues.Count >= 40)
                break;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length && issues.Count < 40; i++)
            {
                var line = lines[i];
                if (!LogCall.IsMatch(line) || !Sensitive.IsMatch(line))
                    continue;

                var snippet = line.Trim();
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-LOG-SENSITIVE-001",
                    RuleName = nameof(SensitiveLoggingRule),
                    Severity = "High",
                    Category = "Logging",
                    Title = "Sensitive value in a log statement",
                    FilePath = file,
                    LineNumber = i + 1,
                    Confidence = "Medium",
                    Cwe = "CWE-532",
                    DetectionMethod = "Static analysis",
                    Description = "A logging call on this line also mentions a password, secret, or similar value.",
                    Impact = "Log files and telemetry then contain the secret for anyone who can read them.",
                    Remediation = "Log an event name or a stable identifier. Do not interpolate secrets into the message.",
                    SecureExample = "logger.LogInformation(\"Login failed for user {UserId}\", user.Id);",
                    Evidence = snippet.Length <= 180 ? snippet : snippet[..180],
                    References = "OWASP A09:2021",
                    Status = "Open"
                });
            }
        }

        return issues;
    }
}
