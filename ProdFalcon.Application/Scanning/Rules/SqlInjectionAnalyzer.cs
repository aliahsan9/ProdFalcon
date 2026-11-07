using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class SqlInjectionAnalyzer
{
    private static readonly (Regex Pattern, string Title, string Severity)[] Patterns =
    [
        (new(@"ExecuteSqlRaw(?:Async)?\s*\(\s*\$""", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Interpolated string passed to ExecuteSqlRaw", "Critical"),
        (new(@"FromSqlRaw\s*\(\s*\$""", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Interpolated string passed to FromSqlRaw", "Critical"),
        (new(@"ExecuteSqlRaw(?:Async)?\s*\([^;\n]*\+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "String concatenation passed to ExecuteSqlRaw", "Critical"),
        (new(@"FromSqlRaw\s*\([^;\n]*\+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "String concatenation passed to FromSqlRaw", "Critical"),
        (new(@"CommandText\s*=\s*\$""[^""\n]*\b(SELECT|INSERT|UPDATE|DELETE|EXEC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL command text built with string interpolation", "High"),
        (new(@"\$""[^""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^""\n]*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with an interpolated string", "High"),
        (new(@"f['""][^'""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^'""\n]*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with a Python f-string", "High"),
        (new(@"\.(?:query|execute)\(\s*[`'""][^`'""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^`'""\n]*\$\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with a template literal", "High")
    ];

    private static readonly Regex ConcatenatedSql = new(
        @"\b(SELECT|INSERT|UPDATE|DELETE|EXEC)\b[^;\n]{0,180}\+\s*[A-Za-z_]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith('#') ||
                trimmed.StartsWith('*') ||
                trimmed.StartsWith("--", StringComparison.Ordinal))
                continue;

            if (IsParameterized(line))
                continue;

            foreach (var (pattern, title, severity) in Patterns)
            {
                if (!pattern.IsMatch(line))
                    continue;

                issues.Add(Create(filePath, i + 1, title, severity));
                break;
            }

            if (issues.Count > 0 && issues[^1].LineNumber == i + 1)
                continue;

            if (ConcatenatedSql.IsMatch(line) && !IsLiteralConcatenation(line))
                issues.Add(Create(filePath, i + 1, "SQL statement concatenated with a variable", "High"));
        }

        return issues;
    }

    private static bool IsParameterized(string line)
    {
        if (line.Contains("ExecuteSqlInterpolated", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("FromSqlInterpolated", StringComparison.OrdinalIgnoreCase))
            return !line.Contains("ExecuteSqlRaw", StringComparison.OrdinalIgnoreCase) &&
                   !line.Contains("FromSqlRaw", StringComparison.OrdinalIgnoreCase);

        if (line.Contains("SqlParameter", StringComparison.Ordinal) ||
            line.Contains("AddWithValue", StringComparison.Ordinal))
            return !line.Contains('+', StringComparison.Ordinal);

        return false;
    }

    private static bool IsLiteralConcatenation(string line) =>
        Regex.IsMatch(line, @"['""][^'""]*['""]\s*\+\s*['""]");

    private static ScanIssue Create(string filePath, int line, string title, string severity) =>
        new()
        {
            RuleId = "SEC-SQLI-001",
            RuleName = nameof(SqlInjectionRiskRule),
            Title = title,
            Severity = severity,
            Category = "Injection",
            FilePath = filePath,
            LineNumber = line,
            Confidence = "High",
            Cwe = "CWE-89",
            DetectionMethod = "Static analysis",
            Description = "Untrusted data appears to be combined into a SQL statement. Parameterized queries were not identified on this line.",
            Impact = "An attacker who controls the concatenated value may read or modify data beyond the intended query.",
            Remediation = "Use parameterized queries, ExecuteSqlInterpolated, or the ORM query API. Keep identifiers such as table names out of user input.",
            SecureExample = "var user = await db.Users.FromSqlInterpolated($\"SELECT * FROM Users WHERE Id = {id}\").FirstOrDefaultAsync();",
            Evidence = "Unsafe SQL construction pattern. Query text is not copied into the finding.",
            References = "https://owasp.org/www-community/attacks/SQL_Injection",
            Status = "Open"
        };
}
