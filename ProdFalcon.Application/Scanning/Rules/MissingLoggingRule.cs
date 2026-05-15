using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class MissingLoggingRule : IScanRule
{
    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();

        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        var files = Directory.GetFiles(projectPath, "*Controller*.cs", SearchOption.AllDirectories);

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

            bool hasLogging =
                content.Contains("ILogger", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("LogInformation", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("LogError", StringComparison.OrdinalIgnoreCase);

            if (!hasLogging)
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-LOG-001",
                    RuleName = nameof(MissingLoggingRule),
                    Severity = "Low",
                    Title = "Missing logging in controller/service",
                    FilePath = file,
                    Description = "No logging mechanism detected in controller."
                });
            }
        }

        return issues;
    }
}