using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public static class SsrfAnalyzer
{
    private readonly record struct Sink(Regex Pattern, string Title, string Severity, string Confidence);

    private static readonly Sink[] Sinks =
    [
        new(new(@"\.(?:GetAsync|GetStringAsync|PostAsync|PutAsync|DeleteAsync|SendAsync|GetStreamAsync)\s*\(\s*\$['""](?:https?://)?\{", RegexOptions.Compiled),
            "HttpClient URL host is interpolated", "High", "High"),
        new(new(@"\.(?:GetAsync|GetStringAsync|PostAsync|PutAsync|DeleteAsync|SendAsync)\s*\(\s*[^;\n]*\+\s*[A-Za-z_]", RegexOptions.Compiled),
            "HttpClient URL is concatenated", "High", "High"),
        new(new(@"\.(?:GetAsync|GetStringAsync|PostAsync|PutAsync|DeleteAsync|SendAsync)\s*\(\s*[A-Za-z_][\w]*(?:[Uu]rl|[Uu]ri|[Hh]ost|[Tt]arget|[Cc]allback|[Ww]ebhook|[Ee]ndpoint)\b", RegexOptions.Compiled),
            "HttpClient receives a URL variable", "High", "Medium"),
        new(new(@"\.(?:DownloadString|DownloadData|UploadString|OpenRead)\s*\(\s*(?:\$['""](?:https?://)?\{|[^;\n]*\+\s*[A-Za-z_]|[A-Za-z_][\w]*(?:[Uu]rl|[Uu]ri))", RegexOptions.Compiled),
            "WebClient uses a dynamic URL", "High", "Medium"),
        new(new(@"WebRequest\.Create(?:Http)?\s*\(\s*(?:\$['""](?:https?://)?\{|[^;\n]*\+\s*[A-Za-z_]|[A-Za-z_][\w]*(?:[Uu]rl|[Uu]ri))", RegexOptions.Compiled),
            "WebRequest targets a dynamic URL", "High", "Medium"),
        new(new(@"\brequests\.(?:get|post|put|delete|head|request)\s*\(\s*f['""](?:https?://)?\{", RegexOptions.Compiled),
            "requests call interpolates the URL", "High", "High"),
        new(new(@"\brequests\.(?:get|post|put|delete|head)\s*\([^)\n]*\+\s*", RegexOptions.Compiled),
            "requests call concatenates the URL", "High", "High"),
        new(new(@"\brequests\.(?:get|post|put|delete|head)\s*\(\s*(?:request\.(?:args|form|values|data)|url|target_url|callback)\b", RegexOptions.Compiled),
            "requests call takes the URL from the request", "High", "High"),
        new(new(@"\bfetch\s*\(\s*(?:req|request)\.", RegexOptions.Compiled),
            "fetch URL comes from the request", "High", "High"),
        new(new(@"\bfetch\s*\(\s*`[^`\n]*\$\{(?:req|request|url|target|host)", RegexOptions.Compiled),
            "fetch template uses a request or host value", "High", "Medium"),
        new(new(@"\.(?:getForObject|getForEntity|exchange|postForObject)\s*\([^)\n]*\+", RegexOptions.Compiled),
            "RestTemplate URL is concatenated", "High", "Medium"),
        new(new(@"\bfile_get_contents\s*\(\s*\$_(GET|POST|REQUEST)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "file_get_contents reads a request URL", "Critical", "High"),
        new(new(@"\bnew\s+(?:URL|URI)\s*\(\s*(?:request\.|req\.|[A-Za-z_][\w]*(?:[Uu]rl|[Uu]ri|[Hh]ost))", RegexOptions.Compiled),
            "URL object is built from request data", "Medium", "Medium")
    ];

    public static List<ScanIssue> AnalyzeFile(string filePath, string content)
    {
        var issues = new List<ScanIssue>();
        if (string.IsNullOrWhiteSpace(content))
            return issues;

        var lines = content.Split('\n');
        var offset = 0;
        var inBlock = false;
        var seen = new HashSet<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineStart = offset;
            offset += line.Length + 1;
            if (LineFilter.Skip(line, ref inBlock))
                continue;

            foreach (var sink in Sinks)
            {
                var match = sink.Pattern.Match(line);
                if (!match.Success || !seen.Add(i + 1))
                    continue;

                var index = lineStart + match.Index;
                var location = EvidenceLocator.Locate(content, index);
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-SSRF-001",
                    RuleName = nameof(SsrfRiskRule),
                    Title = sink.Title,
                    Severity = sink.Severity,
                    Category = "ServerSideRequest",
                    FilePath = filePath,
                    LineNumber = location.Line,
                    ColumnNumber = location.Column,
                    Confidence = sink.Confidence,
                    Cwe = "CWE-918",
                    DetectionMethod = "Static analysis",
                    Description = "The outbound URL is built from a variable, concatenation, or request data. A fixed host with only a path parameter is a weaker case and still needs an allow-list.",
                    Impact = "If a caller controls the host, the server can be made to request internal addresses, cloud metadata, or other systems the caller cannot reach directly. Reachability was not proven.",
                    Remediation = "Allow-list schemes and hosts. Resolve the host and reject private, link-local, and metadata addresses before the request is sent. Do not pass the raw request URL to an HTTP client.",
                    SecureExample = "if (!AllowedHosts.Contains(uri.Host)) return BadRequest(); await client.GetAsync(uri);",
                    Evidence = EvidenceLocator.Format(filePath, content, index),
                    References = "OWASP A10:2021 https://owasp.org/Top10/A10_2021-Server-Side_Request_Forgery_%28SSRF%29/",
                    Status = "Open"
                });
                break;
            }
        }

        return issues;
    }
}

public class SsrfRiskRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Ssrf;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".js" or ".ts" or ".py" or ".java" or ".php"))
                continue;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(SsrfAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
