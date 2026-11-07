using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Quality;

/// <summary>
/// Structural findings with file and line evidence. Stylistic preferences are not reported.
/// </summary>
public static class CodeQualityAnalyzer
{
    private static readonly string[] ControlKeywords = ["if", "for", "foreach", "while", "case", "catch", "&&", "||", "??"];

    public static List<ScanIssue> Analyze(string projectPath)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return issues;

        var files = SourceTree.EnumerateSourceFiles(projectPath)
            .Where(path => Path.GetExtension(path) is ".cs" or ".ts" or ".js")
            .Where(path => !IsTestPath(path))
            .Take(800)
            .ToList();

        var windows = new Dictionary<string, (string File, int Line)>(StringComparer.Ordinal);
        var duplicateCount = 0;
        var complexityCount = 0;
        var nestingCount = 0;
        var largeFileCount = 0;

        foreach (var file in files)
        {
            var content = SourceTree.TryReadTextAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            if (content == null)
                continue;

            var relative = ProjectIntelligenceEngine.Relative(projectPath, file);
            var lines = content.Split('\n');
            if (lines.Length > 800 && largeFileCount < 8)
            {
                largeFileCount++;
                issues.Add(Issue(
                    "QUAL-LARGE-FILE",
                    "Large source file",
                    "Low",
                    "Medium",
                    "Maintainability",
                    relative,
                    1,
                    $"This file has {lines.Length} lines. Large files are harder to review and usually mix responsibilities.",
                    "Split the file by responsibility once a boundary is obvious. Do not split it only to satisfy a line count.",
                    "Move an independent type into its own file and keep the original type focused on one reason to change."));
            }

            if (Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                content.Contains(": Controller", StringComparison.Ordinal) &&
                content.Contains("DbContext", StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    "QUAL-CONTROLLER-DB",
                    "Controller references a DbContext directly",
                    "Medium",
                    "Medium",
                    "Architecture",
                    relative,
                    1,
                    "The controller file mentions DbContext. Endpoints that query the database directly are harder to test and tend to accumulate unrelated responsibilities (Single Responsibility).",
                    "Move the query behind an existing application service. Keep authorization in the controller.",
                    "public Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(_orders.List(cancellationToken));"));
            }

            AnalyzeMethods(relative, lines, issues, ref complexityCount, ref nestingCount);
            if (duplicateCount < 8)
                CollectDuplicates(relative, lines, windows, issues, ref duplicateCount);
        }

        issues.AddRange(FindCircularProjectReferences(projectPath));
        return issues;
    }

    private static void AnalyzeMethods(
        string relative,
        string[] lines,
        List<ScanIssue> issues,
        ref int complexityCount,
        ref int nestingCount)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!LooksLikeMethod(lines[i]))
                continue;

            if (!TryMethodSpan(lines, i, out var start, out var end))
                continue;

            var complexity = 1;
            var maxNesting = 0;
            var nesting = 0;
            for (var lineIndex = start; lineIndex <= end; lineIndex++)
            {
                var line = StripComment(lines[lineIndex]);
                nesting += line.Count(character => character == '{') - line.Count(character => character == '}');
                if (nesting > maxNesting)
                    maxNesting = nesting;
                foreach (var keyword in ControlKeywords)
                {
                    if (line.Contains(keyword, StringComparison.Ordinal))
                        complexity++;
                }
            }

            if (complexity >= 25 && complexityCount < 12)
            {
                complexityCount++;
                var severity = complexity >= 40 ? "High" : "Medium";
                issues.Add(Issue(
                    "QUAL-COMPLEXITY",
                    "High cyclomatic complexity",
                    severity,
                    "Medium",
                    "Maintainability",
                    relative,
                    start + 1,
                    $"A brace-based estimate counted about {complexity} decision points. This is an approximation, not a compiler metric.",
                    "Pull decision tables or validation into a separate method and return early for invalid states.",
                    "if (!IsValid(request)) return BadRequest();\nreturn Ok(await _service.HandleAsync(request, cancellationToken));"));
            }

            if (maxNesting >= 6 && nestingCount < 10)
            {
                nestingCount++;
                issues.Add(Issue(
                    "QUAL-NESTING",
                    "Deep nesting",
                    "Medium",
                    "Medium",
                    "Maintainability",
                    relative,
                    start + 1,
                    $"Brace depth reaches {maxNesting}. Deep nesting makes the failure path hard to see.",
                    "Return early for guard conditions so the main path stays flat.",
                    "if (user is null) return NotFound();\nif (!user.IsActive) return Forbid();"));
            }

            i = end;
        }
    }

    private static void CollectDuplicates(
        string relative,
        string[] lines,
        Dictionary<string, (string File, int Line)> windows,
        List<ScanIssue> issues,
        ref int duplicateCount)
    {
        var normalized = lines.Select(StripComment).ToArray();
        for (var i = 0; i + 6 < normalized.Length && duplicateCount < 8; i++)
        {
            var window = normalized.Skip(i).Take(6).ToArray();
            if (window.Any(line => line.Length < 12) || window.All(line => line is "{" or "}" or ""))
                continue;

            var joined = string.Join("\n", window);
            if (joined.Length < 80)
                continue;

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)))[..16];
            if (windows.TryGetValue(hash, out var first) && !first.File.Equals(relative, StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
                issues.Add(Issue(
                    "QUAL-DUPLICATE",
                    "Duplicated code block",
                    "Low",
                    "Medium",
                    "Maintainability",
                    relative,
                    i + 1,
                    $"Six consecutive non-trivial lines match {first.File}:{first.Line}. The match is textual, so generated or repeated boilerplate can still be a false positive.",
                    "If the blocks change for the same reason, move them behind one method. Leave them if they only look similar.",
                    "SharedMapper.ToDto(entity);"));
                windows.Remove(hash);
            }
            else
            {
                windows.TryAdd(hash, (relative, i + 1));
            }
        }
    }

    public static List<ScanIssue> FindCircularProjectReferences(string projectPath)
    {
        var issues = new List<ScanIssue>();
        var projects = SourceTree.EnumerateSourceFiles(projectPath, "*.csproj").Take(40).ToList();
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            graph[name] = [];
            try
            {
                var document = XDocument.Load(project);
                foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
                {
                    var include = reference.Attribute("Include")?.Value;
                    if (string.IsNullOrWhiteSpace(include))
                        continue;
                    graph[name].Add(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')));
                }
            }
            catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
            {
                continue;
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();

        void Visit(string node)
        {
            if (!graph.ContainsKey(node) || !visited.Add(node))
                return;
            visiting.Add(node);
            stack.Add(node);
            foreach (var next in graph[node])
            {
                if (visiting.Contains(next))
                {
                    var cycle = string.Join(" -> ", stack.Append(next));
                    issues.Add(Issue(
                        "QUAL-CIRCULAR-PROJECT",
                        "Circular project reference",
                        "High",
                        "High",
                        "Architecture",
                        node + ".csproj",
                        1,
                        "Project references form a cycle: " + cycle + ". Circular projects cannot be reasoned about as a one-way dependency.",
                        "Extract the shared types into a third project that both sides reference, and remove one direction of the cycle.",
                        "<ProjectReference Include=\"..\\Shared\\Shared.csproj\" />"));
                    continue;
                }

                Visit(next);
            }

            visiting.Remove(node);
            stack.RemoveAt(stack.Count - 1);
        }

        foreach (var node in graph.Keys)
            Visit(node);

        return issues.Take(5).ToList();
    }

    private static bool TryMethodSpan(string[] lines, int signature, out int start, out int end)
    {
        start = signature;
        end = signature;
        var depth = 0;
        var opened = false;
        for (var i = signature; i < lines.Length && i < signature + 400; i++)
        {
            depth += lines[i].Count(character => character == '{');
            if (lines[i].Contains('{'))
            {
                opened = true;
                start = i;
            }

            depth -= lines[i].Count(character => character == '}');
            if (opened && depth <= 0)
            {
                end = i;
                return end > start;
            }
        }

        return false;
    }

    private static bool LooksLikeMethod(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length is 0 or > 220)
            return false;
        if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*') || trimmed.StartsWith('['))
            return false;
        return Regex.IsMatch(trimmed, @"^(public|private|protected|internal).+\(.*\)\s*(\{|=>)?$")
            && !trimmed.Contains(" class ", StringComparison.Ordinal)
            && !trimmed.Contains(" record ", StringComparison.Ordinal)
            && !trimmed.Contains(" interface ", StringComparison.Ordinal);
    }

    private static string StripComment(string line)
    {
        var trimmed = line.Trim();
        var comment = trimmed.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            trimmed = trimmed[..comment].Trim();
        return trimmed;
    }

    private static bool IsTestPath(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static ScanIssue Issue(
        string ruleId,
        string title,
        string severity,
        string confidence,
        string category,
        string file,
        int line,
        string description,
        string remediation,
        string example) => new()
    {
        RuleId = ruleId,
        RuleName = ruleId,
        Title = title,
        Severity = severity,
        Confidence = confidence,
        Category = category,
        FilePath = file,
        LineNumber = line,
        Description = description,
        Impact = "This raises the cost and risk of future changes. It is not, by itself, a vulnerability.",
        Remediation = remediation,
        SecureExample = example,
        DetectionMethod = "Static analysis",
        Status = "Detected"
    };
}
