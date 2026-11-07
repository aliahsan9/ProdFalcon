using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class CodeQualityRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Quality;

    private static readonly Regex MethodSignature = new(
        @"^\s*(public|private|protected|internal).+\(.*\)\s*$",
        RegexOptions.Compiled);

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        var qualityCount = 0;

        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            if (content.Contains("BinaryFormatter", StringComparison.Ordinal) ||
                content.Contains("TypeNameHandling.All", StringComparison.Ordinal))
            {
                issues.Add(new ScanIssue
                {
                    RuleId = "SEC-DESER-001",
                    RuleName = nameof(CodeQualityRule),
                    Title = "Unsafe deserialization API",
                    Severity = "High",
                    Category = "Security",
                    FilePath = file,
                    LineNumber = 1,
                    Confidence = "High",
                    Cwe = "CWE-502",
                    DetectionMethod = "Static analysis",
                    Description = "BinaryFormatter or TypeNameHandling.All can deserialize attacker-controlled types.",
                    Impact = "Unsafe deserializers have been used to execute attacker-controlled payloads.",
                    Remediation = "Remove BinaryFormatter and avoid TypeNameHandling.All. Use a serializer with a fixed type list.",
                    SecureExample = "var dto = JsonSerializer.Deserialize<ReportDto>(json);",
                    Status = "Open"
                });
            }

            if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || qualityCount >= 20)
                continue;

            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length && qualityCount < 20; i++)
            {
                if (!MethodSignature.IsMatch(lines[i]))
                    continue;

                var depth = 0;
                var started = false;
                var end = i;
                for (var j = i; j < lines.Length; j++)
                {
                    depth += lines[j].Count(c => c == '{') - lines[j].Count(c => c == '}');
                    if (lines[j].Contains('{'))
                        started = true;
                    if (started && depth <= 0)
                    {
                        end = j;
                        break;
                    }
                }

                var length = end - i;
                if (length > 80)
                {
                    qualityCount++;
                    issues.Add(new ScanIssue
                    {
                        RuleId = "QUAL-SIZE-001",
                        RuleName = nameof(CodeQualityRule),
                        Title = "Large method",
                        Severity = "Low",
                        Category = "Quality",
                        FilePath = file,
                        LineNumber = i + 1,
                        Confidence = "Medium",
                        DetectionMethod = "Static analysis",
                        Description = $"This method spans about {length} lines. This is a maintainability warning, not a confirmed vulnerability.",
                        Impact = "Large methods are harder to review and more likely to hide behavior changes.",
                        Remediation = "Split the method into smaller units with a single responsibility.",
                        Status = "Open"
                    });
                }
            }
        }

        return issues;
    }
}
