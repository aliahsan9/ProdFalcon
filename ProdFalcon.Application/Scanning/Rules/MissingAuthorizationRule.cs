using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class MissingAuthorizationRule : IScanRule
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

            // Heuristic: controller exists but no authorization attribute
            if (content.Contains("Controller") &&
                !content.Contains("[Authorize]"))
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-AUTH-001",
                    RuleName = nameof(MissingAuthorizationRule),
                    Severity = "High",
                    Title = "Missing [Authorize] attribute detected on controller",
                    FilePath = file,
                    Description = "Controller appears to lack authorization protection."
                });
            }
        }

        return issues;
    }
}