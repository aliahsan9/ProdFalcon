using System.Text;
using ProdFalcon.Application.Scanning.Dependencies;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Remediation;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Infrastructure.Reporting;

namespace ProdFalcon.Tests;

public class SecurityEngineExpansionTests
{
    [Fact]
    public void Sql_formats_and_php_concatenation_are_cited_with_a_column()
    {
        var formatted = SqlInjectionAnalyzer.AnalyzeFile("Repo.cs",
            "var sql = string.Format(\"SELECT * FROM Users WHERE Name = {0}\", name);");
        var php = SqlInjectionAnalyzer.AnalyzeFile("Repo.php",
            "$sql = \"SELECT * FROM Users WHERE Name = \" . $name;");
        var safe = SqlInjectionAnalyzer.AnalyzeFile("Repo.py",
            "cursor.execute(\"SELECT * FROM Users WHERE Id = %s\", (user_id,))");

        var finding = Assert.Single(formatted);
        Assert.Equal("SEC-SQLI-001", finding.RuleId);
        Assert.True(finding.ColumnNumber > 1);
        Assert.Contains($":{finding.LineNumber}:{finding.ColumnNumber} |", finding.Evidence);
        Assert.NotEmpty(php);
        Assert.Empty(safe);
    }

    [Fact]
    public void New_secret_formats_are_redacted_and_placeholders_stay_quiet()
    {
        const string token = "npm_1234567890abcdefghijKLMNOPQRSTUVWXYZ";
        var issues = SecretsAnalyzer.AnalyzeFile(".env", $"NPM_TOKEN={token}\nPASSWORD=changeme\n");

        var finding = Assert.Single(issues);
        Assert.Equal("Exposed npm token", finding.Title);
        Assert.DoesNotContain(token, finding.Evidence, StringComparison.Ordinal);
        Assert.Contains(".env:1:", finding.Evidence);
        Assert.True(finding.ColumnNumber > 0);
    }

    [Fact]
    public void Xss_csrf_ssrf_and_auth_checks_cite_the_matching_line()
    {
        var xss = XssAnalyzer.AnalyzeFile("app.ts", "this.sanitizer.bypassSecurityTrustHtml(comment);");
        var cleanXss = XssAnalyzer.AnalyzeFile("app.ts", "element.textContent = comment;");
        var csrf = CsrfAnalyzer.AnalyzeFile("AccountController.cs", "[IgnoreAntiforgeryToken]\npublic class AccountController : Controller { }");
        var ssrf = SsrfAnalyzer.AnalyzeFile("Client.cs", "await client.GetAsync(targetUrl);");
        var fixedHost = SsrfAnalyzer.AnalyzeFile("Client.cs", "await client.GetAsync(\"https://example.com/health\");");
        var auth = AuthenticationHardeningAnalyzer.AnalyzeFile("Jwt.cs", "options.TokenValidationParameters.ValidateIssuer = false;");

        Assert.Contains(xss, issue => issue.Cwe == "CWE-79" && issue.ColumnNumber > 0 && issue.Evidence.Contains("app.ts:1:"));
        Assert.Empty(cleanXss);
        Assert.Contains(csrf, issue => issue.Cwe == "CWE-352" && issue.LineNumber == 1);
        Assert.Contains(ssrf, issue => issue.Cwe == "CWE-918" && issue.Evidence.Contains("Client.cs:1:"));
        Assert.Empty(fixedHost);
        Assert.Contains(auth, issue => issue.RuleId == "SEC-AUTH-002" && issue.LineNumber == 1);
    }

    [Fact]
    public void Mvc_post_without_antiforgery_is_flagged_and_api_controllers_are_not()
    {
        const string mvc = """
            public class HomeController : Controller
            {
                [HttpPost]
                public IActionResult Save(Item item) => View();
            }
            """;
        const string api = """
            public class ItemsController : ControllerBase
            {
                [HttpPost]
                public IActionResult Save(Item item) => Ok();
            }
            """;

        Assert.Contains(CsrfAnalyzer.AnalyzeFile("HomeController.cs", mvc), issue => issue.RuleId == "SEC-CSRF-002");
        Assert.DoesNotContain(CsrfAnalyzer.AnalyzeFile("ItemsController.cs", api), issue => issue.RuleId == "SEC-CSRF-002");
    }

    [Fact]
    public void Bundled_advisories_use_the_manifest_line_and_ignore_fixed_versions()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-dep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "package.json"), """
                {
                  "dependencies": {
                    "lodash": "^4.17.20",
                    "react": "^18.2.0"
                  }
                }
                """);
            File.WriteAllText(Path.Combine(root, "pom.xml"), """
                <project>
                  <dependencies>
                    <dependency>
                      <groupId>org.apache.logging.log4j</groupId>
                      <artifactId>log4j-core</artifactId>
                      <version>2.14.1</version>
                    </dependency>
                    <dependency>
                      <groupId>org.apache.logging.log4j</groupId>
                      <artifactId>log4j-core</artifactId>
                      <version>2.17.1</version>
                    </dependency>
                  </dependencies>
                </project>
                """);

            var packages = DependencyManifestReader.Read(root);
            var lodash = Assert.Single(packages, package => package.Name == "lodash");
            Assert.Equal("4.17.20", lodash.Version);
            Assert.True(lodash.LineNumber > 1);
            Assert.Contains(BundledAdvisoryCatalog.Match(lodash), hit => hit.Cve == "CVE-2021-23337");
            Assert.Empty(BundledAdvisoryCatalog.Match(Assert.Single(packages, package => package.Name == "react")));

            var vulnerable = Assert.Single(packages, package => package.Declared == "2.14.1");
            var fixedLog = Assert.Single(packages, package => package.Declared == "2.17.1");
            Assert.Contains(BundledAdvisoryCatalog.Match(vulnerable), hit => hit.Cve == "CVE-2021-44228");
            Assert.Empty(BundledAdvisoryCatalog.Match(fixedLog));
            Assert.Contains("pom.xml", vulnerable.ManifestPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Remediation_draft_explains_the_finding_and_does_not_keep_a_secret()
    {
        var draft = RemediationComposer.Draft(new ScanIssue
        {
            Title = "Hardcoded password",
            FilePath = "Auth.cs",
            LineNumber = 3,
            ColumnNumber = 9,
            Cwe = "CWE-798",
            Description = "A password is stored in source.",
            Impact = "Anyone with the file can reuse it.",
            Remediation = "Load the password from configuration.",
            SecureExample = "var password = configuration[\"Password\"];",
            Evidence = "Auth.cs:3:9 | var password = \"[redacted]\";",
            DetectionMethod = "Pattern and entropy"
        });

        Assert.Contains("Auth.cs:3:9", draft.Explanation);
        Assert.Contains("configuration[\"Password\"]", draft.PatchedSnippet);
        Assert.DoesNotContain("super-secret-value", draft.DiffText, StringComparison.Ordinal);
        Assert.Contains("repository was not modified", draft.DiffText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Audit_pdf_names_the_finding_and_its_location()
    {
        var pdf = ScanReportWriter.WritePdf(new ScanResultDto
        {
            ProjectName = "Demo",
            OverallScore = 70,
            RiskLevel = "High",
            FileCount = 2,
            Issues =
            [
                new ScanIssueSummaryDto
                {
                    Title = "Interpolated SQL",
                    Severity = "High",
                    FilePath = "Repo.cs",
                    LineNumber = 4,
                    ColumnNumber = 9,
                    Description = "SQL is interpolated.",
                    Remediation = "Use parameters.",
                    SecureExample = "command.Parameters.AddWithValue(\"@id\", id);",
                    Evidence = "Repo.cs:4:9 | var sql = SELECT"
                }
            ]
        });

        var text = Encoding.ASCII.GetString(pdf);
        Assert.Contains("PRODFALCON SECURITY AUDIT", text);
        Assert.Contains("Interpolated SQL", text);
        Assert.Contains("Repo.cs:4:9", text);
        Assert.Contains("Suggested patch", text);
    }
}
