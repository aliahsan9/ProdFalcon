using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class HardcodedJwtSecretRule : IScanRule
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

            if (content.Contains("JWT_SECRET", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("jwtSecret", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("eyJ")) // naive JWT detection
            {
                issues.Add(new ScanIssue
                {
                    RuleName = nameof(HardcodedJwtSecretRule),
                    Severity = "High",
                    Title = "Hardcoded JWT secret detected",
                    FilePath = file,
                    Description = "Potential JWT secret or token hardcoded in source code."
                });
            }
        }

        return issues;
    }
}