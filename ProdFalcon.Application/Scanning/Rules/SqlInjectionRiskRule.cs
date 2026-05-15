using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class SqlInjectionRiskRule : IScanRule
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

            var lower = content; // avoid ToLower() allocation issues

            bool possibleSqlInjection =
                lower.Contains("select ", StringComparison.OrdinalIgnoreCase) &&
                (
                    lower.Contains("' or ", StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains("= '", StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains("--", StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains("union select", StringComparison.OrdinalIgnoreCase)
                );

            if (possibleSqlInjection)
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-SQLI-001",
                    RuleName = nameof(SqlInjectionRiskRule),
                    Severity = "Critical",
                    Title = "Potential SQL Injection vulnerability detected",
                    FilePath = file,
                    Description = "Possible unsafe SQL query construction detected. Consider using parameterized queries."
                });
            }
        }

        return issues;
    }
}