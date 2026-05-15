using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class PlainTextPasswordRule : IScanRule
{
    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();

        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        var files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
                continue;

            // safer heuristic detection
            bool looksLikePassword =
                content.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("=") &&
                !content.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("encrypt", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("securestring", StringComparison.OrdinalIgnoreCase);

            if (looksLikePassword)
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-PASS-001",
                    RuleName = nameof(PlainTextPasswordRule),
                    Severity = "Critical",
                    Title = "Possible plaintext password detected",
                    FilePath = file,
                    Description = "A potential plaintext password assignment was found in source code."
                });
            }
        }

        return issues;
    }
}