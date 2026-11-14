using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class SecretsAnalyzer
{
    private static readonly (Regex Pattern, string Category, string Severity, string Cwe, string Title)[] Patterns =
    [
        (new(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled), "AWS access key", "Critical", "CWE-798", "Exposed AWS access key"),
        (new(@"\bghp_[A-Za-z0-9]{20,}\b", RegexOptions.Compiled), "GitHub personal access token", "Critical", "CWE-798", "Exposed GitHub token"),
        (new(@"\bgithub_pat_[A-Za-z0-9_]{20,}\b", RegexOptions.Compiled), "GitHub fine-grained token", "Critical", "CWE-798", "Exposed GitHub token"),
        (new(@"\bsk_live_[0-9a-zA-Z]{16,}\b", RegexOptions.Compiled), "Stripe live secret key", "Critical", "CWE-798", "Exposed Stripe live key"),
        (new(@"\bsk_test_[0-9a-zA-Z]{16,}\b", RegexOptions.Compiled), "Stripe test secret key", "Medium", "CWE-798", "Exposed Stripe test key"),
        (new(@"\bAIza[0-9A-Za-z\-_]{30,}\b", RegexOptions.Compiled), "Google API key", "High", "CWE-798", "Exposed Google API key"),
        (new(@"\bxox[baprs]-[A-Za-z0-9-]{10,}\b", RegexOptions.Compiled), "Slack token", "High", "CWE-798", "Exposed Slack token"),
        (new(@"-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----", RegexOptions.Compiled), "Private key", "Critical", "CWE-321", "Private key committed to source"),
        (new(@"\bnpm_[A-Za-z0-9]{36,}\b", RegexOptions.Compiled), "npm access token", "Critical", "CWE-798", "Exposed npm token"),
        (new(@"\bglpat-[A-Za-z0-9\-_]{20,}\b", RegexOptions.Compiled), "GitLab personal access token", "Critical", "CWE-798", "Exposed GitLab token"),
        (new(@"\bsk-(?:proj-)?[A-Za-z0-9]{20,}\b", RegexOptions.Compiled), "OpenAI API key", "Critical", "CWE-798", "Exposed OpenAI API key"),
        (new(@"\bSG\.[A-Za-z0-9_\-]{16,}\.[A-Za-z0-9_\-]{16,}\b", RegexOptions.Compiled), "SendGrid API key", "High", "CWE-798", "Exposed SendGrid API key"),
        (new(@"hooks\.slack\.com/services/T[A-Z0-9]+/B[A-Z0-9]+/[A-Za-z0-9]+", RegexOptions.Compiled), "Slack webhook", "High", "CWE-798", "Exposed Slack webhook"),
        (new(@"AccountKey\s*=\s*[A-Za-z0-9+/=]{20,}", RegexOptions.Compiled), "Azure storage key", "Critical", "CWE-798", "Exposed Azure storage key"),
        (new(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.Compiled), "JSON Web Token", "High", "CWE-798", "Encoded JWT committed to source")
    ];

    private static readonly Regex AssignedSecret = new(
        @"\b(api[_-]?key|secret|access[_-]?token|client[_-]?secret|jwt[_-]?secret|signing[_-]?key)\b\s*[:=]\s*['""]([^'""\r\n]{12,})['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PasswordAssignment = new(
        @"\b(password|passwd|pwd)\b\s*[:=]\s*['""]([^'""\r\n]{6,})['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ConnectionPassword = new(
        @"\b(?:Password|Pwd)\s*=\s*([^;""'\s]{4,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnvironmentSecret = new(
        @"^(?:export\s+)?(?<name>AWS_SECRET_ACCESS_KEY|AWS_SECRET|OPENAI_API_KEY|GITHUB_TOKEN|GH_TOKEN|NPM_TOKEN|SLACK_BOT_TOKEN|DATABASE_PASSWORD|SECRET_KEY|JWT_SECRET)\s*=\s*['""]?(?<value>[^\s'""#]{8,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        foreach (var (pattern, category, severity, cwe, title) in Patterns)
        {
            foreach (Match match in pattern.Matches(content))
            {
                if (IsCommentAt(content, match.Index))
                    continue;

                var ruleId = title.Contains("JWT", StringComparison.Ordinal) ? "SEC-JWT-001" : "SEC-SECRET-001";
                var ruleName = title.Contains("JWT", StringComparison.Ordinal) ? nameof(HardcodedJwtSecretRule) : nameof(ApiKeyExposureRule);
                issues.Add(Create(filePath, content, match.Index, title, severity, category, cwe, match.Value, "High", ruleId, ruleName));
            }
        }

        foreach (Match match in AssignedSecret.Matches(content))
        {
            var value = match.Groups[2].Value;
            if (SecretRedactor.IsPlaceholder(value) || ShannonEntropy(value) < 3.4)
                continue;

            var name = match.Groups[1].Value;
            var jwt = name.Contains("jwt", StringComparison.OrdinalIgnoreCase) ||
                      name.Contains("signing", StringComparison.OrdinalIgnoreCase);
            issues.Add(Create(
                filePath, content, match.Index,
                jwt ? "Hardcoded JWT signing key" : "Hardcoded secret assignment",
                "High",
                name,
                "CWE-798",
                value,
                "Medium",
                jwt ? "SEC-JWT-001" : "SEC-SECRET-001",
                jwt ? nameof(HardcodedJwtSecretRule) : nameof(ApiKeyExposureRule)));
        }

        foreach (Match match in PasswordAssignment.Matches(content))
        {
            var value = match.Groups[2].Value;
            if (SecretRedactor.IsPlaceholder(value))
                continue;

            issues.Add(Create(
                filePath, content, match.Index,
                "Hardcoded password",
                "High",
                "Password",
                "CWE-798",
                value,
                "High",
                "SEC-PWD-001",
                nameof(PlainTextPasswordRule)));
        }

        foreach (Match match in ConnectionPassword.Matches(content))
        {
            var value = match.Groups[1].Value;
            if (SecretRedactor.IsPlaceholder(value))
                continue;

            if (!content.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(Create(
                filePath, content, match.Index,
                "Connection string contains a password",
                "High",
                "Database credential",
                "CWE-798",
                value,
                "High",
                "SEC-CONN-001",
                nameof(HardcodedConnectionStringRule)));
        }

        foreach (Match match in EnvironmentSecret.Matches(content))
        {
            var value = match.Groups["value"].Value;
            if (SecretRedactor.IsPlaceholder(value) || IsCommentAt(content, match.Index))
                continue;

            var line = SourceTree.LineNumberAt(content, match.Index);
            if (issues.Exists(issue => issue.LineNumber == line))
                continue;

            issues.Add(Create(
                filePath, content, match.Index,
                "Secret assigned in an environment-style file",
                "High",
                match.Groups["name"].Value,
                "CWE-798",
                value,
                "Medium",
                "SEC-SECRET-001",
                nameof(ApiKeyExposureRule)));
        }

        return Dedupe(issues);
    }

    private static bool IsCommentAt(string content, int index)
    {
        var start = index <= 0 ? 0 : content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = content.IndexOf('\n', index);
        if (end < 0)
            end = content.Length;

        var line = content[start..end].TrimStart();
        return line.StartsWith("//", StringComparison.Ordinal) ||
               line.StartsWith('#') ||
               line.StartsWith("--", StringComparison.Ordinal) ||
               line.StartsWith('*') ||
               line.StartsWith("/*", StringComparison.Ordinal);
    }

    private static List<ScanIssue> Dedupe(List<ScanIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<ScanIssue>();
        foreach (var issue in issues)
        {
            var key = $"{issue.LineNumber}|{issue.Title}";
            if (seen.Add(key))
                unique.Add(issue);
        }

        return unique;
    }

    private static ScanIssue Create(
        string filePath,
        string content,
        int index,
        string title,
        string severity,
        string category,
        string cwe,
            string secret,
            string confidence,
            string ruleId,
            string ruleName)
    {
        var location = EvidenceLocator.Locate(content, index);
        return new()
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Title = title,
            Severity = severity,
            Category = "Secrets",
            FilePath = filePath,
            LineNumber = location.Line,
            ColumnNumber = location.Column,
            Confidence = confidence,
            Cwe = cwe,
            DetectionMethod = "Pattern and entropy",
            Description = $"A {category} appears to be stored in source. The value was redacted.",
            Evidence = EvidenceLocator.Format(filePath, content, index, secret) + " " + SecretRedactor.Describe(category, secret),
            Impact = "Anyone with repository or artifact access can reuse this credential.",
            Remediation = "Revoke the exposed credential, remove it from source and history, and load it from a secret store or environment variable at runtime.",
            SecureExample = "var apiKey = configuration[\"Integrations:ApiKey\"];",
            References = "OWASP A07:2021 https://owasp.org/www-community/vulnerabilities/Use_of_hard-coded_password",
            Status = "Open"
        };
    }

    internal static double ShannonEntropy(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        var groups = value.GroupBy(c => c);
        double entropy = 0;
        foreach (var group in groups)
        {
            var p = (double)group.Count() / value.Length;
            entropy -= p * Math.Log(p, 2);
        }

        return entropy;
    }
}
