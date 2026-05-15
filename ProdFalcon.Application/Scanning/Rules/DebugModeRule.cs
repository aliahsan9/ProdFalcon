using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class DebugModeRule : IScanRule
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

            if (content.Contains("DEBUG") || content.Contains("IsDevelopment()"))
            {
                issues.Add(new ScanIssue
                {
                    Title = "Debug mode detected",
                    Severity = "Medium",
                    FilePath = file,
                    Description = "Debug or development mode logic found in production code."
                });
            }
        }

        return issues;
    }
}