using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class HttpUsageRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.StaticAnalysis;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("http://", StringComparison.OrdinalIgnoreCase) || IsBenign(lines[i]))
                    continue;

                var snippet = lines[i].Trim();
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-HTTP-001",
                    RuleName = nameof(HttpUsageRule),
                    Severity = "Medium",
                    Category = "Transport",
                    Title = "Cleartext HTTP URL",
                    FilePath = file,
                    LineNumber = i + 1,
                    Confidence = "Medium",
                    Cwe = "CWE-319",
                    DetectionMethod = "Static analysis",
                    Description = "This line contains an http:// URL that is not a schema, namespace, or local address.",
                    Impact = "Traffic to that URL is visible and modifiable on the network.",
                    Remediation = "Use https:// for external endpoints. Keep local-only development addresses out of production configuration.",
                    Evidence = snippet.Length <= 180 ? snippet : snippet[..180],
                    References = "OWASP A02:2021",
                    Status = "Open"
                });
                break;
            }
        }

        return issues;
    }

    private static bool IsBenign(string line)
    {
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) ||
            line.TrimStart().StartsWith('*') ||
            line.TrimStart().StartsWith('#'))
            return true;

        return line.Contains("schemas.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("www.w3.org", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("json-schema.org", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("xmlns", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("example.com", StringComparison.OrdinalIgnoreCase);
    }
}
