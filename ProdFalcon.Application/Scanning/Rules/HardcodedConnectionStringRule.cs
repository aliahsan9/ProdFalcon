using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class HardcodedConnectionStringRule : IScanRule
{
    private static readonly Regex ConnectionStringPattern =
        new(@"Server=.*;Database=.*;User Id=.*;Password=.*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            if (ConnectionStringPattern.IsMatch(content))
            {
                issues.Add(new ScanIssue
                {
                    Title = "Hardcoded connection string detected",
                    Severity = "High",
                    FilePath = file,
                    Description = "A full database connection string is hardcoded in source code."
                });
            }
        }

        return issues;
    }
}