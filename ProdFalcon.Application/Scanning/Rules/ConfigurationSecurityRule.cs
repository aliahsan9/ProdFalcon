using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class ConfigurationSecurityRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Configuration;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase))
            {
                var hasFrom = content.Contains("FROM ", StringComparison.OrdinalIgnoreCase);
                var runsAsRoot = RegexHas(content, @"^\s*USER\s+root\b");
                var hasUser = RegexHas(content, @"^\s*USER\s+\S+");
                if (hasFrom && runsAsRoot)
                {
                    issues.Add(Issue(file, "Container explicitly runs as root", "High", "USER root",
                        "Running as root increases the impact of a container breakout.",
                        "Create an unprivileged user and end the Dockerfile with USER.",
                        "RUN useradd --create-home appuser\nUSER appuser"));
                }
                else if (hasFrom && !hasUser)
                {
                    issues.Add(Issue(file, "Dockerfile does not set a non-root user", "Medium", "Missing USER",
                        "The image inherits the default root user.",
                        "Add a dedicated user and a USER instruction after files are copied.",
                        "USER appuser"));
                }
            }

            if (name.Contains("docker-compose", StringComparison.OrdinalIgnoreCase) || name.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase))
            {
                if (content.Contains("privileged: true", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Issue(file, "Compose service is privileged", "High", "privileged: true",
                        "A privileged container has broad access to the host.",
                        "Remove privileged mode unless a documented host integration requires it.",
                        "privileged: false"));
                }
            }

            if (name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("Development", StringComparison.OrdinalIgnoreCase))
            {
                if (content.Contains("\"AllowedHosts\": \"*\"", StringComparison.Ordinal) ||
                    content.Contains("\"AllowedHosts\":\"*\"", StringComparison.Ordinal))
                {
                    issues.Add(Issue(file, "Host filtering allows any host", "Low", "AllowedHosts is *",
                        "The application will accept requests addressed to any host header.",
                        "Set AllowedHosts to the production host names.",
                        "\"AllowedHosts\": \"app.example.com\""));
                }
            }
        }

        return issues;
    }

    private static bool RegexHas(string content, string pattern) =>
        System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static ScanIssue Issue(string file, string title, string severity, string evidence, string impact, string remediation, string example) =>
        new()
        {
            RuleId = "SEC-CONFIG-001",
            RuleName = nameof(ConfigurationSecurityRule),
            Title = title,
            Severity = severity,
            Category = "Configuration",
            FilePath = file,
            Confidence = "High",
            Cwe = "CWE-16",
            DetectionMethod = "Configuration review",
            Description = evidence,
            Evidence = evidence,
            Impact = impact,
            Remediation = remediation,
            SecureExample = example,
            Status = "Open"
        };
}
