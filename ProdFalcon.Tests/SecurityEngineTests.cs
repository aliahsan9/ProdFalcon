using System.IO.Compression;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Security;
using ProdFalcon.Application.Scanning.Services;

namespace ProdFalcon.Tests;

public class SecurityEngineTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/runtime", "dotnet", "runtime")]
    [InlineData("https://github.com/dotnet/runtime.git", "dotnet", "runtime")]
    [InlineData("https://github.com/dotnet/runtime/tree/main", "dotnet", "runtime")]
    public void Accepts_public_github_repository_urls(string url, string owner, string name)
    {
        Assert.True(GitHubUrlParser.TryParse(url, out var reference, out var error), error);
        Assert.Equal(owner, reference!.Owner);
        Assert.Equal(name, reference.Name);
    }

    [Theory]
    [InlineData("http://github.com/dotnet/runtime")]
    [InlineData("https://user:token@github.com/dotnet/runtime")]
    [InlineData("https://github.com.evil.com/dotnet/runtime")]
    [InlineData("https://127.0.0.1/dotnet/runtime")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://github.com/dotnet")]
    public void Rejects_unsafe_repository_urls(string url)
    {
        Assert.False(GitHubUrlParser.TryParse(url, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Detects_a_dummy_api_key_without_storing_it()
    {
        const string secret = "AKIAIOSFODNN7EXAMPLE";
        var issues = SecretsAnalyzer.AnalyzeFile("config.cs", $"var key = \"{secret}\";");

        var finding = Assert.Single(issues);
        Assert.Equal("Critical", finding.Severity);
        Assert.DoesNotContain(secret, finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, finding.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_password_placeholders()
    {
        var issues = SecretsAnalyzer.AnalyzeFile("appsettings.json", "\"password\": \"changeme\"");
        Assert.DoesNotContain(issues, issue => issue.Title.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Flags_concatenated_sql_and_ignores_parameterized_sql()
    {
        var vulnerable = SqlInjectionAnalyzer.AnalyzeFile("UserRepository.cs",
            "var sql = \"SELECT * FROM Users WHERE Name = \" + name;");
        var safe = SqlInjectionAnalyzer.AnalyzeFile("UserRepository.cs",
            "command.CommandText = \"SELECT * FROM Users WHERE Id = @id\";");
        var interpolated = SqlInjectionAnalyzer.AnalyzeFile("UserRepository.cs",
            "await db.Database.ExecuteSqlRawAsync($\"SELECT * FROM Users WHERE Name = {name}\");");

        Assert.NotEmpty(vulnerable);
        Assert.Empty(safe);
        Assert.Contains(interpolated, issue => issue.Cwe == "CWE-89");
    }

    [Fact]
    public void Rejects_zip_entries_that_escape_the_destination()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "bad.zip");
        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../evil.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("nope");
            }

            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Validate(zipPath, new ZipSafetyLimits()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Score_uses_open_findings_only()
    {
        var scoring = new RiskScoringService();
        var open = scoring.Calculate([Finding("Critical", "Open")]);
        var resolved = scoring.Calculate([Finding("Critical", "Resolved")]);

        Assert.Equal(75, open.OverallScore);
        Assert.Equal("Critical", open.RiskLevel);
        Assert.Equal(100, resolved.OverallScore);
    }

    [Fact]
    public void Fingerprint_is_stable_for_the_same_issue()
    {
        var first = FindingFingerprint.Create("SEC-SECRET-001", "src\\config.cs", 12, "Exposed AWS access key");
        var second = FindingFingerprint.Create("SEC-SECRET-001", "src/config.cs", 12, "Exposed AWS access key");
        Assert.Equal(first, second);
    }

    private static ScanIssue Finding(string severity, string status) =>
        new()
        {
            Severity = severity,
            Status = status,
            Category = "Secrets",
            Confidence = "High"
        };
}
