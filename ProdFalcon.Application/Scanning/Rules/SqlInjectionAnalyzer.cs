using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class SqlInjectionAnalyzer
{
    private readonly record struct SqlPattern(Regex Pattern, string Title, string Severity, string Languages);

    private static readonly SqlPattern[] Patterns =
    [
        new(new(@"ExecuteSqlRaw(?:Async)?\s*\(\s*\$""", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Interpolated string passed to ExecuteSqlRaw", "Critical", "cs"),
        new(new(@"FromSqlRaw\s*\(\s*\$""", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Interpolated string passed to FromSqlRaw", "Critical", "cs"),
        new(new(@"ExecuteSqlRaw(?:Async)?\s*\([^;\n]*\+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "String concatenation passed to ExecuteSqlRaw", "Critical", "cs"),
        new(new(@"FromSqlRaw\s*\([^;\n]*\+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "String concatenation passed to FromSqlRaw", "Critical", "cs"),
        new(new(@"ExecuteSqlRaw(?:Async)?\s*\(\s*[A-Za-z_]", RegexOptions.Compiled),
            "ExecuteSqlRaw receives a non-literal command", "High", "cs"),
        new(new(@"FromSqlRaw\s*\(\s*[A-Za-z_]", RegexOptions.Compiled),
            "FromSqlRaw receives a non-literal command", "High", "cs"),
        new(new(@"string\.Format\s*\(\s*[@$]*['""][^'""\n]*\b(SELECT|INSERT|UPDATE|DELETE|EXEC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with string.Format", "High", "cs"),
        new(new(@"String\.format\s*\(\s*['""][^'""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b", RegexOptions.Compiled),
            "SQL statement built with String.format", "High", "java"),
        new(new(@"CommandText\s*=\s*\$""[^""\n]*\b(SELECT|INSERT|UPDATE|DELETE|EXEC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL command text built with string interpolation", "High", "cs"),
        new(new(@"\$""[^""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^""\n]*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with an interpolated string", "High", "any"),
        new(new(@"f['""][^'""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^'""\n]*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with a Python f-string", "High", "py"),
        new(new(@"cursor\.execute\s*\(\s*['""][^'""\n]*['""]\s*%", RegexOptions.Compiled),
            "cursor.execute formats SQL with the % operator", "High", "py"),
        new(new(@"cursor\.execute\s*\([^)\n]*\.format\s*\(", RegexOptions.Compiled),
            "cursor.execute formats SQL with str.format", "High", "py"),
        new(new(@"(?:mysqli_query|mysql_query|->query)\s*\([^)\n]*\.\s*\$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "PHP query built by concatenation", "High", "php"),
        new(new(@"\.(?:query|execute)\(\s*[`'""][^`'""\n]*\b(SELECT|INSERT|UPDATE|DELETE)\b[^`'""\n]*\$\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "SQL statement built with a template literal", "High", "js")
    ];

    private static readonly Regex ConcatenatedSql = new(
        @"\b(SELECT|INSERT|UPDATE|DELETE|EXEC)\b[^;\n]{0,180}\+\s*[A-Za-z_]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhpConcatenatedSql = new(
        @"\b(SELECT|INSERT|UPDATE|DELETE)\b[^;\n]{0,180}\.\s*\$[A-Za-z_]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var extension = Path.GetExtension(filePath);
        var language = Language(extension);
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

            if (IsParameterized(line))
                continue;

            Match? hit = null;
            var title = string.Empty;
            var severity = "High";
            foreach (var pattern in Patterns)
            {
                if (!Applies(pattern.Languages, language))
                    continue;

                var match = pattern.Pattern.Match(line);
                if (!match.Success)
                    continue;

                hit = match;
                title = pattern.Title;
                severity = pattern.Severity;
                break;
            }

            if (hit == null && ConcatenatedSql.IsMatch(line) && !IsLiteralConcatenation(line))
            {
                hit = ConcatenatedSql.Match(line);
                title = "SQL statement concatenated with a variable";
            }

            if (hit == null && language == "php" && PhpConcatenatedSql.IsMatch(line))
            {
                hit = PhpConcatenatedSql.Match(line);
                title = "SQL statement concatenated with a PHP variable";
            }

            if (hit == null)
                continue;

            issues.Add(Create(filePath, content, lineStart + hit.Index, title, severity));
        }

        return issues;
    }

    private static string Language(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".cs" => "cs",
            ".py" => "py",
            ".java" => "java",
            ".php" => "php",
            ".js" or ".ts" => "js",
            _ => "any"
        };

    private static bool Applies(string languages, string language) =>
        languages == "any" || languages == language;

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

    private static ScanIssue Create(string filePath, string content, int index, string title, string severity)
    {
        var location = EvidenceLocator.Locate(content, index);
        return new ScanIssue
        {
            RuleId = "SEC-SQLI-001",
            RuleName = nameof(SqlInjectionRiskRule),
            Title = title,
            Severity = severity,
            Category = "Injection",
            FilePath = filePath,
            LineNumber = location.Line,
            ColumnNumber = location.Column,
            Confidence = "High",
            Cwe = "CWE-89",
            DetectionMethod = "Static analysis",
            Description = "Untrusted data appears to be combined into a SQL statement. Parameterized queries were not identified on this line.",
            Impact = "An attacker who controls the concatenated value may read or modify data beyond the intended query. That can expose or change rows the statement was not meant to touch.",
            Remediation = "Use parameterized queries, ExecuteSqlInterpolated, or the ORM query API. Keep identifiers such as table names out of user input.",
            SecureExample = "var user = await db.Users.FromSqlInterpolated($\"SELECT * FROM Users WHERE Id = {id}\").FirstOrDefaultAsync();",
            Evidence = EvidenceLocator.Format(filePath, content, index),
            References = "OWASP A03:2021 https://owasp.org/www-community/attacks/SQL_Injection",
            Status = "Open"
        };
    }
}
