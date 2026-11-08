using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class DebugModeRule : IScanRule, IScanModule
{
    private static readonly Regex JsonDebug = new(@"""Debug""\s*:\s*true", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string ModuleKey => ScanModules.Configuration;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var name = Path.GetFileName(file);
            var developmentConfig = name.Contains("Development", StringComparison.OrdinalIgnoreCase);
            if (!developmentConfig &&
                (name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("web.config", StringComparison.OrdinalIgnoreCase)) &&
                (JsonDebug.IsMatch(content) || content.Contains("<compilation debug=\"true\"", StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Issue(file, content, "Debug configuration is enabled",
                    "A base configuration file turns debug compilation or debug output on.",
                    "Debug pages and symbols expose internals that production traffic should not see.",
                    "Set debug to false in the production configuration and keep development-only settings in an environment-specific file."));
                continue;
            }

            if (Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("UseDeveloperExceptionPage(", StringComparison.Ordinal) &&
                !content.Contains("IsDevelopment()", StringComparison.Ordinal))
            {
                issues.Add(Issue(file, content, "Developer exception page is not limited to development",
                    "UseDeveloperExceptionPage is registered without an IsDevelopment check in this file.",
                    "The developer page returns exception details to whoever can call the API.",
                    "Call UseDeveloperExceptionPage only when the host environment is Development."));
            }
        }

        return issues;
    }

    private static ScanIssue Issue(string file, string content, string title, string description, string impact, string remediation)
    {
        var line = 1;
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("UseDeveloperExceptionPage", StringComparison.Ordinal) ||
                lines[i].Contains("debug", StringComparison.OrdinalIgnoreCase))
            {
                line = i + 1;
                break;
            }
        }

        return new ScanIssue
        {
            RuleId = "SEC-DEBUG-001",
            RuleName = nameof(DebugModeRule),
            Title = title,
            Severity = "Medium",
            Category = "Configuration",
            FilePath = file,
            LineNumber = line,
            Confidence = "Medium",
            Cwe = "CWE-489",
            DetectionMethod = "Static analysis",
            Description = description,
            Impact = impact,
            Remediation = remediation,
            Evidence = lines[line - 1].Trim(),
            References = "OWASP A05:2021",
            Status = "Open"
        };
    }
}
