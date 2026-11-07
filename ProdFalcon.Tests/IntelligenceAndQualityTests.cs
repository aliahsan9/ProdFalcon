using System.Security.Cryptography;
using System.Text;
using ProdFalcon.Application.Scanning.Copilot;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Application.Scanning.Intelligence;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Performance;
using ProdFalcon.Application.Scanning.Quality;
using ProdFalcon.Application.Scanning.Remediation;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Security;

namespace ProdFalcon.Tests;

public class IntelligenceAndQualityTests
{
    [Fact]
    public void Profile_separates_detected_stack_from_inferred_purpose()
    {
        var root = Temp();
        try
        {
            File.WriteAllText(Path.Combine(root, "App.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
                  </ItemGroup>
                </Project>
                """);
            Directory.CreateDirectory(Path.Combine(root, "Controllers"));
            File.WriteAllText(Path.Combine(root, "Controllers", "OrdersController.cs"), """
                public class OrdersController : ControllerBase
                {
                    [HttpGet("orders")]
                    public IActionResult List() => Ok();
                }
                """);

            var profile = ProjectIntelligenceEngine.Analyze(root);
            Assert.Contains(profile.Languages, language => language == "C#");
            Assert.Contains(profile.Technologies, item => item.Name == "ASP.NET Core");
            Assert.Contains(profile.Endpoints, endpoint => endpoint.Route == "orders");
            Assert.Contains(profile.Databases, item => item.Contains("SQL Server", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Inferred", profile.PurposeKind);
            Assert.DoesNotContain(profile.Facts, fact => fact.Kind == "Inferred");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Architecture_does_not_invent_a_call_that_is_not_in_source()
    {
        var root = Temp();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Api"));
            File.WriteAllText(Path.Combine(root, "Api", "OrdersController.cs"), "public class OrdersController : ControllerBase { }");
            File.WriteAllText(Path.Combine(root, "Api", "BillingService.cs"), "public class BillingService { }");
            var profile = ProjectIntelligenceEngine.Analyze(root);
            var graph = ArchitectureGraphBuilder.Build(root, profile);
            Assert.Contains(graph.Nodes, node => node.Label == "OrdersController");
            Assert.DoesNotContain(graph.Edges, edge => edge.Kind == "Detected" && edge.Target.Contains("BillingService", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Quality_and_security_patterns_distinguish_vulnerable_and_secure_samples()
    {
        var vulnerable = Temp();
        var secure = Temp();
        try
        {
            WritePair(vulnerable, secure);
            var vulnerableIssues = Scan(vulnerable);
            var secureIssues = Scan(secure);

            var expected = new[]
            {
                "SEC-SQLI-001",
                "REL-SYNC-BLOCK",
                "SEC-UPLOAD-NAME",
                "DEP-NEWTONSOFT-21907",
                "QUAL-CIRCULAR-PROJECT",
                "QUAL-COMPLEXITY"
            };

            var truePositives = expected.Count(rule => vulnerableIssues.Any(issue => issue.RuleId == rule || issue.RuleId == "SEC-SECRET-001" && false));
            truePositives = expected.Count(rule => vulnerableIssues.Any(issue => issue.RuleId == rule));
            var falseNegatives = expected.Length - truePositives;
            var falsePositives = expected.Count(rule => secureIssues.Any(issue => issue.RuleId == rule));
            var precision = truePositives + falsePositives == 0 ? 1 : (double)truePositives / (truePositives + falsePositives);
            var recall = (double)truePositives / expected.Length;

            var missing = string.Join(", ", expected.Where(rule => vulnerableIssues.All(issue => issue.RuleId != rule)));
            Assert.True(truePositives == expected.Length, "Missing rules: " + missing + ". Found: " + string.Join(", ", vulnerableIssues.Select(issue => issue.RuleId).Distinct()));
            Assert.Equal(0, falseNegatives);
            Assert.Equal(0, falsePositives);
            Assert.Equal(1, precision);
            Assert.Equal(1, recall);
            Assert.Contains(vulnerableIssues, issue => issue.Title.Contains("AWS", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(secureIssues, issue => issue.Title.Contains("AWS", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(vulnerable, true);
            Directory.Delete(secure, true);
        }
    }

    [Fact]
    public void Resolved_finding_is_verified_only_when_a_later_scan_does_not_report_it()
    {
        var tracked = new List<TrackedFinding>
        {
            new() { Fingerprint = "fixed", Status = FindingStatuses.Resolved, Title = "Secret" },
            new() { Fingerprint = "still", Status = FindingStatuses.Resolved, Title = "SQL" }
        };
        var current = new List<ScanIssue>
        {
            new() { Fingerprint = "still", Title = "SQL", Severity = "High", FilePath = "a.cs", RuleId = "SEC-SQLI-001" }
        };

        var comparison = FindingReconciler.Reconcile(tracked, current, 4, "main", "main", "aaa", "bbb", DateTime.UtcNow);

        Assert.Equal(FindingStatuses.Verified, tracked.Single(item => item.Fingerprint == "fixed").Status);
        Assert.Equal(FindingStatuses.Detected, tracked.Single(item => item.Fingerprint == "still").Status);
        Assert.Equal(1, comparison.VerifiedCount);
        Assert.Equal(1, comparison.RecurringCount);
    }

    [Fact]
    public void Workflow_template_preserves_github_expressions()
    {
        var yaml = GitHubWorkflowTemplate.Create("https://example.test");

        Assert.Contains("${{ secrets.PRODFALCON_TOKEN }}", yaml, StringComparison.Ordinal);
        Assert.Contains("${{ github.sha }}", yaml, StringComparison.Ordinal);
        Assert.Contains("${{ github.ref_name }}", yaml, StringComparison.Ordinal);
        Assert.Contains("https://example.test/api/github/ci", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{{{{", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Webhook_signature_rejects_a_tampered_body()
    {
        const string secret = "top-secret";
        const string body = """{"action":"opened"}""";
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        Assert.True(GitHubWebhookVerifier.IsValid(body, "sha256=" + hash, secret));
        Assert.False(GitHubWebhookVerifier.IsValid(body + " ", "sha256=" + hash, secret));
        Assert.False(GitHubWebhookVerifier.IsValid(body, "sha256=abcd", secret));
    }

    [Fact]
    public void Copilot_does_not_invent_a_dependency_upgrade()
    {
        var scan = new ScanResult { Id = 3, Score = 90, Issues = [] };
        var analysis = new ProjectAnalysisDocument
        {
            Intelligence = new ProjectIntelligenceProfile
            {
                Dependencies = [new DependencyFact { Name = "Newtonsoft.Json", Version = "13.0.3" }]
            }
        };

        var answer = SecurityCopilot.Answer("Which dependencies should I upgrade?", scan, analysis);
        Assert.Contains("none are recommended", answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Newtonsoft.Json", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Copilot_lists_production_limits()
    {
        var scan = new ScanResult { Id = 2, Score = 80, SecurityScore = 80, MaintainabilityScore = 90, PerformanceScore = 90, ProductionReadinessScore = 70 };
        var answer = SecurityCopilot.Answer("Is this project ready for production?", scan, new ProjectAnalysisDocument());
        Assert.Contains("not a release approval", answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unverified", answer, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ScanIssue> Scan(string root)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            issues.AddRange(SecretsAnalyzer.AnalyzeFile(file, content));
            issues.AddRange(SqlInjectionAnalyzer.AnalyzeFile(file, content));
        }

        issues.AddRange(CodeQualityAnalyzer.Analyze(root));
        issues.AddRange(PerformanceReliabilityAnalyzer.Analyze(root));
        return issues;
    }

    private static void WritePair(string vulnerable, string secure)
    {
        var branches = string.Join(Environment.NewLine, Enumerable.Range(1, 30).Select(i => $"        if (name == \"{i}\") return;"));
        File.WriteAllText(Path.Combine(vulnerable, "Vulnerable.cs"),
            "public class VulnerableController : ControllerBase\n{\n    public async Task Run(string name, IFormFile file)\n    {\n" +
            "        var key = \"AKIAIOSFODNN7REALKEY\";\n" +
            "        var sql = \"SELECT * FROM Users WHERE Name = \" + name;\n" +
            "        var path = Path.Combine(directory, file.FileName);\n" +
            branches + "\n" +
            "        var rows = query.Result;\n    }\n}\n");
        File.WriteAllText(Path.Combine(vulnerable, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
                <ProjectReference Include="Other.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(vulnerable, "Other.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="App.csproj" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(secure, "Secure.cs"), """
            public class SecureController
            {
                public async Task Run(string name, IFormFile file, CancellationToken cancellationToken)
                {
                    var sql = "SELECT * FROM Users WHERE Name = @name";
                    var stored = Path.Combine(directory, Path.GetFileName(file.FileName));
                    var rows = await query;
                }
            }
            """);
        File.WriteAllText(Path.Combine(secure, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
    }

    [Fact]
    public async Task Public_HelloWorld_archive_is_analyzed_from_its_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-hello-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = Path.Combine(root, "repo.zip");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ProdFalcon-tests");
            await using (var remote = await http.GetStreamAsync("https://codeload.github.com/octocat/Hello-World/zip/refs/heads/master"))
            await using (var local = File.Create(zip))
                await remote.CopyToAsync(local);

            var extracted = Path.Combine(root, "src");
            SafeZipExtractor.Extract(zip, extracted, new ZipSafetyLimits());
            Assert.NotEmpty(Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories));

            var analysis = ProjectAnalysisPipeline.Analyze(extracted);
            Assert.False(string.IsNullOrWhiteSpace(analysis.Profile.Purpose));
            Assert.NotEmpty(analysis.Profile.Answers);
            Assert.Empty(analysis.Profile.Endpoints);
            Assert.DoesNotContain(analysis.Profile.Components, component => component.Type.Contains("Database", StringComparison.OrdinalIgnoreCase));
            Assert.All(analysis.Architecture.Edges, edge => Assert.True(edge.Kind is "Detected" or "Uncertain"));
            Assert.All(analysis.Threats.Threats, threat => Assert.False(threat.ExploitConfirmed));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static string Temp()
    {
        var path = Path.Combine(Path.GetTempPath(), "pf-intel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
