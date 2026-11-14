using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class AuthenticationHardeningAnalyzer
{
    private readonly record struct Check(Regex Pattern, string RuleId, string Title, string Severity, string Confidence, string Cwe, string Description, string Remediation, string Example);

    private static readonly Check[] Checks =
    [
        new(new(@"ValidateIssuer\s*=\s*false\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SEC-AUTH-002", "JWT issuer is not validated", "High", "High", "CWE-287",
            "TokenValidationParameters turns issuer validation off.",
            "Set ValidateIssuer to true and assign the expected issuer.",
            "options.TokenValidationParameters.ValidateIssuer = true;"),
        new(new(@"ValidateAudience\s*=\s*false\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SEC-AUTH-003", "JWT audience is not validated", "High", "High", "CWE-287",
            "TokenValidationParameters turns audience validation off.",
            "Set ValidateAudience to true and assign the expected audience.",
            "options.TokenValidationParameters.ValidateAudience = true;"),
        new(new(@"ValidateLifetime\s*=\s*false\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SEC-AUTH-004", "JWT lifetime is not validated", "High", "High", "CWE-613",
            "Expired tokens are still accepted when lifetime validation is off.",
            "Set ValidateLifetime to true.",
            "options.TokenValidationParameters.ValidateLifetime = true;"),
        new(new(@"ValidateIssuerSigningKey\s*=\s*false\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SEC-AUTH-005", "JWT signing key is not validated", "Critical", "High", "CWE-347",
            "The token signature is not checked against the configured key.",
            "Set ValidateIssuerSigningKey to true and supply the issuer signing key.",
            "options.TokenValidationParameters.ValidateIssuerSigningKey = true;"),
        new(new(@"RequireHttpsMetadata\s*=\s*false\b", RegexOptions.Compiled),
            "SEC-AUTH-006", "OIDC metadata is allowed over HTTP", "Medium", "High", "CWE-319",
            "RequireHttpsMetadata is false, so discovery metadata can be fetched without TLS.",
            "Set RequireHttpsMetadata to true outside local development.",
            "options.RequireHttpsMetadata = true;"),
        new(new(@"(?:Cookie\.)?HttpOnly\s*=\s*false\b", RegexOptions.Compiled),
            "SEC-AUTH-007", "Cookie is readable by script", "Medium", "Medium", "CWE-1004",
            "HttpOnly is set to false. Script on the page can read the cookie if this is the session cookie.",
            "Set HttpOnly to true for session and authentication cookies.",
            "options.Cookie.HttpOnly = true;"),
        new(new(@"CookieSecurePolicy\.None\b", RegexOptions.Compiled),
            "SEC-AUTH-008", "Auth cookie may be sent over HTTP", "Medium", "Medium", "CWE-614",
            "The cookie secure policy is None.",
            "Use CookieSecurePolicy.Always in production.",
            "options.Cookie.SecurePolicy = CookieSecurePolicy.Always;"),
        new(new(@"Request\.Query\[\s*['""](?:token|access_token|jwt)['""]\s*\]|req\.query\.(?:token|access_token)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SEC-AUTH-009", "Bearer token is read from the query string", "High", "High", "CWE-598",
            "A token is taken from the URL. URLs are copied into logs, history, and Referer headers.",
            "Send the token in the Authorization header.",
            "var header = Request.Headers.Authorization.ToString();")
    ];

    private static readonly Regex PasswordEquals = new(
        @"\b(password|passwd)\b\s*==\s*(?!null\b|undefined\b|string\.Empty\b|[""'])([A-Za-z_][\w]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SensitiveAnonymous = new(
        @"\b(Delete|Admin|Impersonate|Grant|ResetPassword|DisableUser)\w*\s*\(",
        RegexOptions.Compiled);

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

            foreach (var check in Checks)
            {
                var match = check.Pattern.Match(line);
                if (!match.Success)
                    continue;

                issues.Add(Create(filePath, content, lineStart + match.Index, check.RuleId, check.Title, check.Severity, check.Confidence, check.Cwe, check.Description, check.Remediation, check.Example));
            }

            var password = PasswordEquals.Match(line);
            if (password.Success && !line.Contains("hash", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Create(filePath, content, lineStart + password.Index, "SEC-AUTH-010",
                    "Password compared with equality", "Medium", "Medium", "CWE-208",
                    "The password is compared with ==. This both stores the comparison in a form that returns early and skips a password hasher.",
                    "Compare a password hash with a hasher that does not require a plain-text equality check. Do not compare raw passwords.",
                    "var valid = hasher.Verify(password, storedHash);"));
            }

            if (line.Contains("[AllowAnonymous", StringComparison.Ordinal))
            {
                var window = string.Join('\n', lines.Skip(i).Take(8));
                var method = SensitiveAnonymous.Match(window);
                if (method.Success)
                {
                    issues.Add(Create(filePath, content, lineStart, "SEC-AUTH-011",
                        "Sensitive action allows anonymous callers", "High", "Medium", "CWE-862",
                        "AllowAnonymous is placed near an action whose name deletes, administers, grants, or resets. Confirm the attribute is not inherited by that action.",
                        "Remove AllowAnonymous from the sensitive action and require an authorization policy.",
                        "[Authorize(Roles = \"Admin\")]\npublic IActionResult Delete(int id) => Ok();"));
                }
            }
        }

        var name = Path.GetFileName(filePath);
        if (name.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase))
        {
            if (content.Contains("UseAuthorization", StringComparison.Ordinal) &&
                !content.Contains("UseAuthentication", StringComparison.Ordinal))
            {
                var index = content.IndexOf("UseAuthorization", StringComparison.Ordinal);
                issues.Add(Create(filePath, content, Math.Max(0, index), "SEC-AUTH-012",
                    "Authorization is used without authentication", "High", "High", "CWE-306",
                    "The pipeline calls UseAuthorization and does not call UseAuthentication. Role checks then see an anonymous user.",
                    "Call UseAuthentication before UseAuthorization.",
                    "app.UseAuthentication();\napp.UseAuthorization();"));
            }
        }

        return Dedupe(issues);
    }

    private static List<ScanIssue> Dedupe(List<ScanIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<ScanIssue>();
        foreach (var issue in issues)
        {
            if (seen.Add(issue.LineNumber + "|" + issue.RuleId))
                unique.Add(issue);
        }

        return unique;
    }

    private static ScanIssue Create(
        string filePath,
        string content,
        int index,
        string ruleId,
        string title,
        string severity,
        string confidence,
        string cwe,
        string description,
        string remediation,
        string example)
    {
        var location = EvidenceLocator.Locate(content, index);
        return new ScanIssue
        {
            RuleId = ruleId,
            RuleName = nameof(AuthenticationHardeningRule),
            Title = title,
            Severity = severity,
            Category = "Authentication",
            FilePath = filePath,
            LineNumber = location.Line,
            ColumnNumber = location.Column,
            Confidence = confidence,
            Cwe = cwe,
            DetectionMethod = "Static analysis",
            Description = description + " This is a configuration indicator, not a completed login bypass.",
            Impact = "Authentication checks that are turned off or tokens that leak into URLs let a caller reuse an identity the application meant to restrict.",
            Remediation = remediation,
            SecureExample = example,
            Evidence = EvidenceLocator.Format(filePath, content, index),
            References = "OWASP A07:2021 https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/",
            Status = "Open"
        };
    }
}

public class AuthenticationHardeningRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Authentication;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".js" or ".ts"))
                continue;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(AuthenticationHardeningAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
