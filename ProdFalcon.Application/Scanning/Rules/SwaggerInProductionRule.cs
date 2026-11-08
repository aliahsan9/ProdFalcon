using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class SwaggerInProductionRule : IScanRule, IScanModule
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
            if (string.IsNullOrWhiteSpace(content))
                continue;

            if (content.Contains("UseSwagger", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("IsDevelopment()", StringComparison.Ordinal))
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC001",
                    RuleName = nameof(SwaggerInProductionRule),
                    Title = "Swagger is enabled in production environment",
                    Severity = "Medium",
                    FilePath = file,
                    LineNumber = 0,
                    Description = "Swagger should be disabled in production environments to avoid exposing API metadata."
                });
            }
        }

        return issues;
    }
}