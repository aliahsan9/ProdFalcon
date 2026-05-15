using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class SensitiveLoggingRule : IScanRule
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

            bool containsSensitiveData =
                content.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("creditcard", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("ssn", StringComparison.OrdinalIgnoreCase);

            bool containsLogging =
                content.Contains("Log", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("ILogger", StringComparison.OrdinalIgnoreCase);

            if (containsSensitiveData && containsLogging)
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-LOG-SENSITIVE-001",
                    RuleName = nameof(SensitiveLoggingRule),
                    Severity = "Critical",
                    Title = "Sensitive information may be logged",
                    FilePath = file,
                    Description = "Logging statements may include sensitive data such as passwords, tokens, or secrets."
                });
            }
        }

        return issues;
    }
}