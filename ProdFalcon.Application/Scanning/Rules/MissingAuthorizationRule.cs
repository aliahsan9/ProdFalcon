using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class MissingAuthorizationRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Authentication;

    private static readonly Regex ActionAttribute = new(
        @"\[(Http(Get|Post|Put|Delete|Patch)|Route)\b",
        RegexOptions.Compiled);

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath, "*Controller*.cs"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content) || !content.Contains("Controller", StringComparison.Ordinal))
                continue;

            var classAuthorized = Regex.IsMatch(content, @"\[Authorize[^\]]*\][\s\S]{0,400}class\s+", RegexOptions.None);
            var lines = content.Split('\n');
            var inMethod = false;
            var methodStart = 0;
            var methodHasAuthorize = false;
            var methodHasAnonymous = false;
            var methodHasAction = false;
            var depth = 0;
            var started = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!inMethod)
                {
                    if (line.Contains("[Authorize", StringComparison.Ordinal))
                        methodHasAuthorize = true;
                    if (line.Contains("[AllowAnonymous", StringComparison.Ordinal))
                        methodHasAnonymous = true;
                    if (ActionAttribute.IsMatch(line))
                        methodHasAction = true;

                    if (line.Contains('{'))
                    {
                        inMethod = methodHasAction;
                        methodStart = i + 1;
                        depth = line.Count(c => c == '{') - line.Count(c => c == '}');
                        started = true;
                        if (!inMethod)
                        {
                            methodHasAuthorize = false;
                            methodHasAnonymous = false;
                            methodHasAction = false;
                        }
                    }

                    continue;
                }

                depth += line.Count(c => c == '{') - line.Count(c => c == '}');
                if (started && depth <= 0)
                {
                    if (methodHasAction && !classAuthorized && !methodHasAuthorize && !methodHasAnonymous)
                    {
                        issues.Add(new ScanIssue
                        {
                            RuleId = "SEC-AUTH-001",
                            RuleName = nameof(MissingAuthorizationRule),
                            Severity = "Medium",
                            Confidence = "Medium",
                            Category = "Authentication",
                            Cwe = "CWE-862",
                            Title = "Endpoint has no authorization attribute",
                            FilePath = file,
                            LineNumber = methodStart,
                            DetectionMethod = "Static analysis",
                            Description = "This action does not declare [Authorize] or [AllowAnonymous], and the controller class is not authorized. Confirm whether anonymous access is intentional.",
                            Impact = "If the endpoint was meant to be private, callers can reach it without an authenticated identity.",
                            Remediation = "Add [Authorize] on the controller or action. Use [AllowAnonymous] only when public access is intentional.",
                            SecureExample = "[Authorize]\n[HttpGet]\npublic IActionResult Get() => Ok();",
                            Status = "Open"
                        });
                    }

                    inMethod = false;
                    methodHasAuthorize = false;
                    methodHasAnonymous = false;
                    methodHasAction = false;
                    started = false;
                }
            }
        }

        return issues;
    }
}
