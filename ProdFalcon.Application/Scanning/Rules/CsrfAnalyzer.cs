using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class CsrfAnalyzer
{
    private static readonly (Regex Pattern, string Title, string Severity, string Confidence, string Remediation, string Example)[] Patterns =
    [
        (new(@"\[IgnoreAntiforgeryToken\b", RegexOptions.Compiled),
            "Anti-forgery validation is disabled", "High", "High",
            "Remove IgnoreAntiforgeryToken from cookie-authenticated form actions. Keep it only on APIs that do not accept cookie credentials.",
            "[ValidateAntiForgeryToken]\n[HttpPost]\npublic IActionResult Save(Item item) => Ok();"),
        (new(@"\.csrf\s*\(\s*\)\s*\.disable\s*\(", RegexOptions.Compiled),
            "CSRF protection is disabled", "High", "High",
            "Leave CSRF protection enabled for browser cookie sessions. Disable it only for a stateless API that does not use cookies.",
            "http.csrf(csrf -> csrf.ignoringRequestMatchers(\"/api/**\"));"),
        (new(@"\bcsrf_exempt\b", RegexOptions.Compiled),
            "Django view is exempt from CSRF", "Medium", "High",
            "Remove csrf_exempt from browser-facing views. Require the CSRF token on state-changing requests.",
            "@require_POST\ndef save(request): ..."),
        (new(@"SameSite(?:Mode)?\.None|sameSite\s*[:=]\s*['""]none['""]|SameSite\s*=\s*None\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Cookie SameSite is None", "Medium", "Medium",
            "Use SameSite=Lax or Strict for session cookies. SameSite=None must also set Secure and still needs a CSRF token for cookie-authenticated writes.",
            "options.Cookie.SameSite = SameSiteMode.Lax;")
    ];

    private static readonly Regex MvcController = new(@":\s*Controller(?!Base)\b", RegexOptions.Compiled);
    private static readonly Regex StateChange = new(@"\[Http(Post|Put|Delete|Patch)\b", RegexOptions.Compiled);
    private static readonly Regex AntiForgery = new(@"\[(ValidateAntiForgeryToken|IgnoreAntiforgeryToken|AutoValidateAntiforgeryToken)\b", RegexOptions.Compiled);

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var lines = content.Split('\n');
        var offset = 0;
        var inBlock = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineStart = offset;
            offset += line.Length + 1;
            if (LineFilter.Skip(line, ref inBlock))
                continue;

            foreach (var (pattern, title, severity, confidence, remediation, example) in Patterns)
            {
                var match = pattern.Match(line);
                if (!match.Success)
                    continue;

                issues.Add(Create(filePath, content, lineStart + match.Index, "SEC-CSRF-001", title, severity, confidence, remediation, example,
                    "The line turns off a CSRF control or marks a cookie as cross-site. Confirm whether the endpoint accepts cookie credentials."));
                break;
            }
        }

        if (!MvcController.IsMatch(content) || content.Contains("[AutoValidateAntiforgeryToken", StringComparison.Ordinal))
            return issues;

        offset = 0;
        inBlock = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineStart = offset;
            offset += line.Length + 1;
            if (LineFilter.Skip(line, ref inBlock) || !StateChange.IsMatch(line))
                continue;

            var windowStart = Math.Max(0, i - 6);
            var window = string.Join('\n', lines.Skip(windowStart).Take(i - windowStart + 4));
            if (AntiForgery.IsMatch(window))
                continue;

            var match = StateChange.Match(line);
            issues.Add(Create(filePath, content, lineStart + match.Index, "SEC-CSRF-002",
                "MVC action changes state without an anti-forgery token", "Medium", "Medium",
                "Add [ValidateAntiForgeryToken] or [AutoValidateAntiforgeryToken] on cookie-authenticated MVC actions. Include the token in the form.",
                "[ValidateAntiForgeryToken]\n[HttpPost]\npublic IActionResult Save(Item item) => View();",
                "This MVC controller action accepts a state-changing verb and the surrounding lines do not declare an anti-forgery attribute. Token-only APIs are a different case."));
        }

        return issues;
    }

    private static ScanIssue Create(
        string filePath,
        string content,
        int index,
        string ruleId,
        string title,
        string severity,
        string confidence,
        string remediation,
        string example,
        string description)
    {
        var location = EvidenceLocator.Locate(content, index);
        return new ScanIssue
        {
            RuleId = ruleId,
            RuleName = nameof(CsrfRiskRule),
            Title = title,
            Severity = severity,
            Category = "RequestForgery",
            FilePath = filePath,
            LineNumber = location.Line,
            ColumnNumber = location.Column,
            Confidence = confidence,
            Cwe = "CWE-352",
            DetectionMethod = "Static analysis",
            Description = description,
            Impact = "A browser that already has the victim's cookie can be induced to submit a state-changing request. The scanner did not execute that request.",
            Remediation = remediation,
            SecureExample = example,
            Evidence = EvidenceLocator.Format(filePath, content, index),
            References = "OWASP A01:2021 https://owasp.org/www-community/attacks/csrf",
            Status = "Open"
        };
    }
}

public class CsrfRiskRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Csrf;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".java" or ".py" or ".js" or ".ts"))
                continue;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(CsrfAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
