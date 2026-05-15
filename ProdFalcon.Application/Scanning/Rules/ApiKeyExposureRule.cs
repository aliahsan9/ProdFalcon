using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using System.Text.RegularExpressions;

namespace ProdFalcon.Application.Scanning.Rules;

public class ApiKeyExposureRule : IScanRule
{
    private static readonly Regex[] Patterns =
    {
        new(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled),                 // AWS Access Key
        new(@"sk_live_[0-9a-zA-Z]{24,}", RegexOptions.Compiled),        // Stripe Live Key
        new(@"AIza[0-9A-Za-z\-_]{35}", RegexOptions.Compiled)           // Google API Key
    };

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

            foreach (var regex in Patterns)
            {
                if (regex.IsMatch(content))
                {
                    issues.Add(new ScanIssue
                    {
                        Title = "Exposed API Key Detected",
                        Severity = "High",
                        FilePath = file,
                        Description = "Potential API key exposure detected in source code."
                    });

                    break; // avoid duplicate matches per file
                }
            }
        }

        return issues;
    }
}