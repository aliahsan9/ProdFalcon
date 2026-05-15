using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class CorsWildcardRule : IScanRule
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
                continue; // skip unreadable files
            }

            if (content.Contains("AllowAnyOrigin()") ||
                content.Contains("WithOrigins(\"*\")"))
            {
                issues.Add(new ScanIssue
                {
                    Title = "Unsafe CORS configuration detected",
                    Severity = "High",
                    FilePath = file,
                    Description = "CORS is configured to allow all origins, which is insecure."
                });
            }
        }

        return issues;
    }
}