using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Services;

public class ProjectScanner : IProjectScanner
{
    private readonly IEnumerable<IScanRule> _rules;

    public ProjectScanner(IEnumerable<IScanRule> rules)
    {
        _rules = rules;
    }

    public async Task<ScanResult> ScanAsync(string projectPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path cannot be empty");

        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"Project path not found: {projectPath}");

        var result = new ScanResult
        {
            Issues = new List<ScanIssue>()
        };

        var files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (IsIgnored(file))
                continue;

            string content;

            try
            {
                content = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch
            {
                continue;
            }

            var context = new ScanContext
            {
                FilePath = file,
                Content = content
            };

            foreach (var rule in _rules)
            {
                var issues = await rule.EvaluateAsync(projectPath, cancellationToken);

                if (issues == null || issues.Count == 0)
                    continue;

                foreach (var issue in issues)
                {
                    result.Issues.Add(new ScanIssue
                    {
                        LineNumber = issue.LineNumber,
                        Description = string.IsNullOrWhiteSpace(issue.Description)
                            ? "No description provided"
                            : issue.Description,

                        RuleId = string.IsNullOrWhiteSpace(issue.RuleId)
                            ? "unknown"
                            : issue.RuleId,

                        RuleName = string.IsNullOrWhiteSpace(issue.RuleName)
                            ? "unknown"
                            : issue.RuleName,

                        Title = string.IsNullOrWhiteSpace(issue.Title)
                            ? "Issue detected"
                            : issue.Title,

                        Severity = string.IsNullOrWhiteSpace(issue.Severity)
                            ? "Info"
                            : issue.Severity,

                        FilePath = file,

                        // DB-related fields (set for persistence)
                        ScanSessionId = 0,
                        ScanSession = null
                    });
                }
            }
        }

        result.Score = CalculateScore(result.Issues);

        return result;
    }

    private static bool IsIgnored(string filePath)
    {
        var ignoredExtensions = new HashSet<string>
        {
            ".dll", ".exe", ".png", ".jpg", ".jpeg",
            ".gif", ".pdf", ".zip", ".bin", ".obj", ".pdb"
        };

        return ignoredExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
    }

    private static int CalculateScore(List<ScanIssue> issues)
    {
        const int baseScore = 100;

        int deduction = issues.Sum(issue =>
        {
            return issue.Severity.ToLower() switch
            {
                "critical" => 25,
                "high" => 15,
                "medium" => 10,
                "low" => 5,
                _ => 5
            };
        });

        return Math.Max(baseScore - deduction, 0);
    }
}