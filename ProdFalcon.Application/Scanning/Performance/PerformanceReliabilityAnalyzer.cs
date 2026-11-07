using System.Text.RegularExpressions;
using System.Xml.Linq;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Performance;

/// <summary>
/// Static predictions only. Nothing in this analyzer measured runtime performance.
/// </summary>
public static class PerformanceReliabilityAnalyzer
{
    private static readonly (string Id, Regex Pattern, string Title, string Severity, string Confidence, string Category, string Cwe, string Description, string Remediation, string Example)[] Patterns =
    [
        (
            "REL-SYNC-BLOCK",
            new Regex(@"\.Result\b|\.Wait\s*\(", RegexOptions.Compiled),
            "Synchronous blocking on a task",
            "High",
            "High",
            "Reliability",
            "",
            "Calling .Result or .Wait() blocks the caller. In ASP.NET Core this can exhaust the thread pool. This is a static prediction, not a measured deadlock.",
            "Await the task and pass CancellationToken through the call.",
            "var orders = await _db.Orders.ToListAsync(cancellationToken);"),
        (
            "REL-ASYNC-VOID",
            new Regex(@"\basync\s+void\b", RegexOptions.Compiled),
            "async void method",
            "Medium",
            "High",
            "Reliability",
            "",
            "async void methods cannot be awaited and their exceptions may crash the process. Event handlers are the usual exception.",
            "Return Task unless this method must match a void event signature.",
            "public async Task SaveAsync(CancellationToken cancellationToken)"),
        (
            "SEC-UPLOAD-NAME",
            new Regex(@"Path\.Combine\([^;\n]*FileName", RegexOptions.Compiled),
            "Upload path uses the client file name",
            "High",
            "Medium",
            "Security",
            "CWE-22",
            "Combining a caller-supplied file name into a path can write outside the intended directory when the name contains directory segments.",
            "Use Path.GetFileName and an allow-list of extensions, then store the file under a generated name.",
            "var name = Path.GetFileName(file.FileName);\nvar stored = Path.Combine(root, Guid.NewGuid() + Path.GetExtension(name));")
    ];

    public static List<ScanIssue> Analyze(string projectPath)
    {
        var issues = new List<ScanIssue>();
        if (!Directory.Exists(projectPath))
            return issues;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath).Where(path => Path.GetExtension(path) is ".cs" or ".ts" or ".js"))
        {
            if (file.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = SourceTree.TryReadTextAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            if (content == null)
                continue;

            var relative = ProjectIntelligenceEngine.Relative(projectPath, file);
            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
                    continue;

                foreach (var pattern in Patterns)
                {
                    if (!pattern.Pattern.IsMatch(lines[i]))
                        continue;
                    if (pattern.Id == "SEC-UPLOAD-NAME" && lines[i].Contains("GetFileName", StringComparison.Ordinal))
                        continue;
                    if (counts.GetValueOrDefault(pattern.Id) >= 12)
                        continue;

                    counts[pattern.Id] = counts.GetValueOrDefault(pattern.Id) + 1;
                    issues.Add(Create(pattern.Id, pattern.Title, pattern.Severity, pattern.Confidence, pattern.Category, pattern.Cwe, relative, i + 1, pattern.Description, pattern.Remediation, pattern.Example));
                }
            }

            InspectQueryPatterns(relative, lines, issues, counts);
            InspectCancellation(relative, lines, issues, counts);
            InspectNode(relative, content, lines, issues, counts);
        }

        InspectDependencyLifetimes(projectPath, issues);
        InspectPackageAdvisories(projectPath, issues);
        return issues;
    }

    private static void InspectQueryPatterns(string relative, string[] lines, List<ScanIssue> issues, Dictionary<string, int> counts)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("foreach", StringComparison.Ordinal))
                continue;

            var window = string.Join('\n', lines.Skip(i).Take(18));
            if (counts.GetValueOrDefault("PERF-NPLUS1") < 8 &&
                Regex.IsMatch(window, @"\.(Where|FirstOrDefault|FirstOrDefaultAsync|Find|ToListAsync|SingleOrDefault)\s*\("))
            {
                counts["PERF-NPLUS1"] = counts.GetValueOrDefault("PERF-NPLUS1") + 1;
                issues.Add(Create(
                    "PERF-NPLUS1",
                    "Possible N+1 query",
                    "Medium",
                    "Low",
                    "Performance",
                    "",
                    relative,
                    i + 1,
                    "A loop is followed by a query-shaped call. This is a static hint that the database may be queried once per item. It was not executed.",
                    "Load the related rows in one query, or use a projection that includes the needed data before the loop.",
                    "var orders = await _db.Orders.Include(o => o.Lines).Where(o => ids.Contains(o.Id)).ToListAsync(cancellationToken);"));
            }
        }

        var text = string.Join('\n', lines);
        if (counts.GetValueOrDefault("PERF-PAGINATION") < 6 &&
            text.Contains("ToListAsync", StringComparison.Ordinal) &&
            text.Contains("DbSet", StringComparison.Ordinal) &&
            !text.Contains("Skip(", StringComparison.Ordinal) &&
            !text.Contains("Take(", StringComparison.Ordinal))
        {
            counts["PERF-PAGINATION"] = counts.GetValueOrDefault("PERF-PAGINATION") + 1;
            issues.Add(Create(
                "PERF-PAGINATION",
                "Query materializes a DbSet without pagination",
                "Low",
                "Low",
                "Performance",
                "",
                relative,
                1,
                "The file materializes query results and does not mention Skip or Take. Unbounded lists become slow as the table grows. This is not a measured query plan.",
                "Accept a page size, apply Skip and Take, and return a total only when the caller needs it.",
                "await query.OrderBy(x => x.Id).Skip(page * size).Take(size).ToListAsync(cancellationToken);"));
        }
    }

    private static void InspectCancellation(string relative, string[] lines, List<ScanIssue> issues, Dictionary<string, int> counts)
    {
        if (!relative.Contains("Controller", StringComparison.OrdinalIgnoreCase) || counts.GetValueOrDefault("REL-CANCELLATION") >= 8)
            return;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("async Task", StringComparison.Ordinal) || lines[i].Contains("CancellationToken", StringComparison.Ordinal))
                continue;
            if (!lines[i].Contains("public", StringComparison.Ordinal))
                continue;

            counts["REL-CANCELLATION"] = counts.GetValueOrDefault("REL-CANCELLATION") + 1;
            issues.Add(Create(
                "REL-CANCELLATION",
                "Async controller action does not accept CancellationToken",
                "Low",
                "Medium",
                "Reliability",
                "",
                relative,
                i + 1,
                "The action is async and its signature does not show a CancellationToken. Clients that disconnect may leave the work running. This does not prove a leak.",
                "Add a CancellationToken parameter and pass it to database and HTTP calls.",
                "public async Task<IActionResult> Get(CancellationToken cancellationToken)"));
            if (counts["REL-CANCELLATION"] >= 8)
                break;
        }
    }

    private static void InspectNode(string relative, string content, string[] lines, List<ScanIssue> issues, Dictionary<string, int> counts)
    {
        if (counts.GetValueOrDefault("PERF-SYNC-IO") >= 6)
            return;
        if (!content.Contains("readFileSync", StringComparison.Ordinal) && !content.Contains("writeFileSync", StringComparison.Ordinal))
            return;
        if (!content.Contains("express", StringComparison.OrdinalIgnoreCase) && !content.Contains("router.", StringComparison.Ordinal))
            return;

        var line = Array.FindIndex(lines, item => item.Contains("readFileSync", StringComparison.Ordinal) || item.Contains("writeFileSync", StringComparison.Ordinal));
        counts["PERF-SYNC-IO"] = counts.GetValueOrDefault("PERF-SYNC-IO") + 1;
        issues.Add(Create(
            "PERF-SYNC-IO",
            "Synchronous filesystem call in a Node handler",
            "Medium",
            "Medium",
            "Performance",
            "",
            relative,
            Math.Max(1, line + 1),
            "A synchronous fs call was found near Express-style routing. It blocks the Node event loop while it runs. No timing was collected.",
            "Use the promise fs API and await it inside the handler.",
            "const text = await fs.promises.readFile(path, 'utf8');"));
    }

    private static void InspectDependencyLifetimes(string projectPath, List<ScanIssue> issues)
    {
        var count = 0;
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath, "*.cs").Take(200))
        {
            var content = SourceTree.TryReadTextAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            if (content == null || !content.Contains("AddSingleton", StringComparison.Ordinal))
                continue;

            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length && count < 5; i++)
            {
                if (!lines[i].Contains("AddSingleton", StringComparison.Ordinal))
                    continue;
                var window = string.Join(' ', lines.Skip(i).Take(3));
                if (!window.Contains("DbContext", StringComparison.Ordinal))
                    continue;

                count++;
                issues.Add(Create(
                    "REL-DBCONTEXT-SINGLETON",
                    "DbContext mentioned with AddSingleton",
                    "High",
                    "Medium",
                    "Reliability",
                    "",
                    ProjectIntelligenceEngine.Relative(projectPath, file),
                    i + 1,
                    "A singleton registration is next to a DbContext reference. DbContext is not thread-safe and is designed to be scoped. Confirm this line is not registering the context itself.",
                    "Register the DbContext with AddDbContext and keep it out of singleton services. Use IServiceScopeFactory when a singleton must do database work.",
                    "services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));"));
            }
        }
    }

    private static void InspectPackageAdvisories(string projectPath, List<ScanIssue> issues)
    {
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath, "*.csproj").Take(30))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(file);
            }
            catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
            {
                continue;
            }

            foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var name = reference.Attribute("Include")?.Value;
                var version = reference.Attribute("Version")?.Value;
                if (!string.Equals(name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase) || !Version.TryParse(Normalize(version), out var parsed))
                    continue;
                if (parsed >= new Version(13, 0, 1))
                    continue;

                issues.Add(Create(
                    "DEP-NEWTONSOFT-21907",
                    "Newtonsoft.Json below 13.0.1",
                    "High",
                    "High",
                    "Security",
                    "CWE-400",
                    ProjectIntelligenceEngine.Relative(projectPath, file),
                    1,
                    "The bundled advisory list matches CVE-2024-21907 for Newtonsoft.Json before 13.0.1 (denial of service). This was not a live NVD query, and reachability was not proven.",
                    "Upgrade the PackageReference to 13.0.1 or later and review TypeNameHandling usage separately.",
                    "<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />"));
            }
        }
    }

    private static string? Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;
        var cleaned = version.Trim().TrimStart('v', 'V');
        var dash = cleaned.IndexOf('-');
        return dash > 0 ? cleaned[..dash] : cleaned;
    }

    private static ScanIssue Create(
        string ruleId,
        string title,
        string severity,
        string confidence,
        string category,
        string cwe,
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
        Cwe = string.IsNullOrWhiteSpace(cwe) ? null : cwe,
        FilePath = file,
        LineNumber = line,
        Description = description,
        Impact = category == "Security"
            ? "The pattern can become a vulnerability if the code path is reachable with attacker-controlled input. Reachability was not proven."
            : "The pattern can reduce throughput or reliability under load. No benchmark was run.",
        Remediation = remediation,
        SecureExample = example,
        DetectionMethod = "Static analysis",
        Status = "Detected"
    };
}
