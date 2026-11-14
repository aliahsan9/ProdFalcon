using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

/// <summary>
/// XSS sinks that the broader OWASP pass does not already cite.
/// A match is a static indicator. It does not prove the value is attacker-controlled.
/// </summary>
public static class XssAnalyzer
{
    private readonly record struct Sink(Regex Pattern, string Extensions, string Title, string Severity, string Confidence, string Remediation, string Example);

    private static readonly Sink[] Sinks =
    [
        new(new(@"bypassSecurityTrust(?:Html|Script|Style|Url|ResourceUrl)\s*\(", RegexOptions.Compiled), ".ts,.js,.html",
            "Angular sanitizer bypass", "High", "High",
            "Keep the value in a normal binding so Angular escapes it. Sanitize with DomSanitizer only for a fixed, reviewed fragment.",
            "<div>{{ comment }}</div>"),
        new(new(@"\bmark_safe\s*\(", RegexOptions.Compiled), ".py,.html",
            "mark_safe disables HTML escaping", "High", "Medium",
            "Pass plain text to the template. Escape untrusted HTML or sanitize it with a reviewed allow-list.",
            "return render(request, \"comment.html\", {\"body\": body})"),
        new(new(@"\bResponse\.Write(?:Async)?\s*\([^;\n]*(\+|\$"")", RegexOptions.Compiled), ".cs,.aspx",
            "Response.Write emits a dynamic string", "High", "Medium",
            "Write encoded text, or return a view that encodes the model.",
            "return Content(message);"),
        new(new(@"\becho\s+.*\$_(GET|POST|REQUEST|COOKIE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), ".php",
            "PHP echo of a request value", "High", "High",
            "Encode the value with htmlspecialchars before it is written into HTML.",
            "echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8');"),
        new(new(@"\{\{[^}\n]*\|\s*safe\b", RegexOptions.Compiled), ".html,.vue,.jinja,.twig",
            "Template marks a value as safe HTML", "Medium", "Medium",
            "Remove the safe filter unless the value was sanitized by a dedicated HTML sanitizer.",
            "{{ comment }}"),
        new(new(@"\[innerHTML\]\s*=", RegexOptions.Compiled), ".html,.ts",
            "Angular innerHTML binding", "Medium", "Medium",
            "Bind the value as text. Use a sanitizer only for an allow-listed fragment.",
            "<div>{{ comment }}</div>"),
        new(new(@"\binsertAdjacentHTML\s*\(", RegexOptions.Compiled), ".js,.ts,.html",
            "insertAdjacentHTML parses markup", "Medium", "Medium",
            "Insert text with textContent or insertAdjacentText.",
            "element.insertAdjacentText('beforeend', name);"),
        new(new(@"\bouterHTML\s*=\s*[^;\n]*(\+|\$\{|\$"")", RegexOptions.Compiled), ".js,.ts,.html",
            "outerHTML built from a dynamic value", "High", "Medium",
            "Assign text with textContent, or build elements with DOM APIs.",
            "element.textContent = name;")
    ];

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return issues;

        var lines = content.Split('\n');
        var offset = 0;
        var inBlock = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineStart = offset;
            offset += line.Length + 1;
            if (LineFilter.Skip(line, ref inBlock))
                continue;

            if (line.Contains("htmlspecialchars", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("htmlentities", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("HtmlEncoder", StringComparison.Ordinal))
                continue;

            foreach (var sink in Sinks)
            {
                if (!Applies(sink.Extensions, extension))
                    continue;

                var match = sink.Pattern.Match(line);
                if (!match.Success)
                    continue;

                var key = sink.Title + "|" + (i + 1);
                if (!seen.Add(key))
                    continue;

                var index = lineStart + match.Index;
                var location = EvidenceLocator.Locate(content, index);
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-XSS-002",
                    RuleName = nameof(XssRiskRule),
                    Title = sink.Title,
                    Severity = sink.Severity,
                    Category = "Injection",
                    FilePath = filePath,
                    LineNumber = location.Line,
                    ColumnNumber = location.Column,
                    Confidence = sink.Confidence,
                    Cwe = "CWE-79",
                    DetectionMethod = "Static analysis",
                    Description = "This line writes a value in a way the browser can parse as HTML. The scanner did not prove that an attacker can supply that value.",
                    Impact = "If any part of the value is attacker-controlled, the browser can run script in the victim's session.",
                    Remediation = sink.Remediation,
                    SecureExample = sink.Example,
                    Evidence = EvidenceLocator.Format(filePath, content, index),
                    References = "OWASP A03:2021 https://owasp.org/www-community/attacks/xss/",
                    Status = "Open"
                });
            }
        }

        return issues;
    }

    private static bool Applies(string extensions, string extension) =>
        extensions.Split(',').Any(item => extension.Equals(item, StringComparison.OrdinalIgnoreCase));
}

public class XssRiskRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Xss;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".js" or ".jsx" or ".ts" or ".tsx" or ".html" or ".vue" or ".php" or ".py" or ".cshtml"))
                continue;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(XssAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
