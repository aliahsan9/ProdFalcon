using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class HttpUsageRule : IScanRule
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

            if (content.Contains("http://", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-HTTP-001",
                    RuleName = nameof(HttpUsageRule),
                    Severity = "Medium",
                    Title = "Insecure HTTP usage detected. Use HTTPS instead.",
                    FilePath = file,
                    Description = "HTTP endpoints found in code which are insecure compared to HTTPS."
                });
            }
        }

        return issues;
    }
}