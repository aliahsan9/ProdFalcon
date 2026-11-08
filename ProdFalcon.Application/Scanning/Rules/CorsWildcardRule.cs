using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class CorsWildcardRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Configuration;
    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();

        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        var files = SourceTree.EnumerateSourceFiles(projectPath);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            if (content.Contains("AllowAnyOrigin()") ||
                content.Contains("WithOrigins(\"*\")"))
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-CORS-001",
                    RuleName = nameof(CorsWildcardRule),
                    Title = "Unsafe CORS configuration detected",
                    Severity = "High",
                    Category = "Configuration",
                    FilePath = file,
                    LineNumber = LineOf(content, "AllowAnyOrigin", "WithOrigins"),
                    Confidence = "High",
                    Cwe = "CWE-942",
                    Description = "CORS is configured to allow all origins.",
                    Impact = "A site the attacker controls can call this API with the victim's browser credentials if credentials are also allowed.",
                    Remediation = "List the trusted origins explicitly. Do not combine a wildcard origin with credentialed requests.",
                    References = "OWASP A05:2021",
                    Status = "Open"
                });
            }
        }

        return issues;
    }

    private static int LineOf(string content, params string[] markers)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (markers.Any(marker => lines[i].Contains(marker, StringComparison.Ordinal)))
                return i + 1;
        }

        return 1;
    }
}