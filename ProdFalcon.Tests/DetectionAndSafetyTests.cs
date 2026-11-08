using System.IO.Compression;
using System.Text;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Security;

namespace ProdFalcon.Tests;

public class DetectionAndSafetyTests
{
    [Fact]
    public void Detects_xss_command_injection_path_traversal_deserialization_and_weak_crypto()
    {
        var xss = OwaspPatternAnalyzer.AnalyzeFile("Views/Comment.cshtml", "@Html.Raw(Model.Body)");
        var command = OwaspPatternAnalyzer.AnalyzeFile("Runner.cs", "Process.Start(\"cmd.exe /c \" + input);");
        var path = OwaspPatternAnalyzer.AnalyzeFile("Files.cs", "var text = File.ReadAllText(root + fileName);");
        var binary = OwaspPatternAnalyzer.AnalyzeFile("Session.cs", "var formatter = new BinaryFormatter();");
        var crypto = OwaspPatternAnalyzer.AnalyzeFile("Hash.cs", "var hash = MD5.Create();");
        var password = OwaspPatternAnalyzer.AnalyzeFile("Auth.cs", "var stored = MD5.HashData(Encoding.UTF8.GetBytes(password));");

        Assert.Contains(xss, issue => issue.Cwe == "CWE-79" && issue.References.Contains("A03:2021"));
        Assert.Contains(command, issue => issue.RuleId == "SEC-CMDI-001" && issue.LineNumber == 1);
        Assert.Contains(path, issue => issue.Cwe == "CWE-22");
        Assert.Contains(binary, issue => issue.Severity == "Critical" && issue.Cwe == "CWE-502");
        Assert.Contains(crypto, issue => issue.RuleId == "SEC-CRYPTO-001");
        Assert.Contains(password, issue => issue.RuleId == "SEC-PWDHASH-001");
        Assert.All(xss.Concat(command).Concat(path), issue => Assert.False(string.IsNullOrWhiteSpace(issue.Evidence)));
    }

    [Fact]
    public void Clean_code_does_not_match_the_owasp_patterns()
    {
        const string clean = """
            var hash = SHA256.HashData(data);
            var text = File.ReadAllText(path);
            Process.Start(fileName);
            var dto = JsonSerializer.Deserialize<Item>(json);
            element.textContent = name;
            subprocess.run([tool, name], check=True)
            """;

        Assert.Empty(OwaspPatternAnalyzer.AnalyzeFile("Clean.cs", clean));
    }

    [Fact]
    public void Ignores_commented_vulnerabilities()
    {
        var issues = OwaspPatternAnalyzer.AnalyzeFile("Notes.cs", "// var formatter = new BinaryFormatter();");
        Assert.Empty(issues);
    }

    [Fact]
    public void Splits_password_and_jwt_findings_from_api_keys()
    {
        var password = SecretsAnalyzer.AnalyzeFile("Auth.cs", "var password = \"N0tAPlaceh0lder!\";");
        var jwt = SecretsAnalyzer.AnalyzeFile("Auth.cs", "var jwtSecret = \"k3Jf9Qw2Lm8Zp4Nc6Vb1Hs7D\";");
        var key = SecretsAnalyzer.AnalyzeFile("Auth.cs", "var key = \"AKIAIOSFODNN7EXAMPLE\";");

        Assert.Contains(password, issue => issue.RuleId == "SEC-PWD-001");
        Assert.DoesNotContain(password, issue => issue.Evidence.Contains("N0tAPlaceh0lder!", StringComparison.Ordinal));
        Assert.Contains(jwt, issue => issue.RuleId == "SEC-JWT-001");
        Assert.Contains(key, issue => issue.RuleId == "SEC-SECRET-001");
    }

    [Fact]
    public void Sensitive_logging_requires_the_secret_on_the_log_line()
    {
        var noisy = SensitiveLog("var password = \"x\";\nlogger.LogInformation(\"User signed in\");");
        var real = SensitiveLog("logger.LogInformation(\"Password reset for {Password}\", password);");

        Assert.Empty(noisy);
        Assert.Contains(real, issue => issue.LineNumber == 1 && issue.Cwe == "CWE-532");
    }

    [Fact]
    public void Http_rule_ignores_schema_urls()
    {
        var issues = Http("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\nvar endpoint = \"http://api.internal.example/orders\";");
        var finding = Assert.Single(issues);
        Assert.Equal(2, finding.LineNumber);
    }

    [Fact]
    public async Task Snapshot_skips_dependency_directories_and_reads_each_file_once()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-snap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules", "pkg"));
        File.WriteAllText(Path.Combine(root, "src", "App.cs"), "class App { }\n");
        File.WriteAllText(Path.Combine(root, "node_modules", "pkg", "index.js"), "module.exports = 1;\n");
        try
        {
            var snapshot = await SourceSnapshotLoader.LoadAsync(root, new ScanLimitsOptions(), CancellationToken.None);
            Assert.Single(snapshot.Files);
            Assert.EndsWith("App.cs", snapshot.Files[0].FullPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(snapshot.SkippedDirectories >= 1);
            using (SourceScanScope.Push(snapshot))
            {
                var first = await SourceTree.TryReadTextAsync(snapshot.Files[0].FullPath, CancellationToken.None);
                var second = await SourceTree.TryReadTextAsync(snapshot.Files[0].FullPath, CancellationToken.None);
                Assert.Equal("class App { }\n", first);
                Assert.Same(first, second);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("bad\0.txt")]
    [InlineData("C:/windows/evil.txt")]
    public void Rejects_null_and_drive_paths_in_zip_entries(string entryName)
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "bad.zip");
        try
        {
            WriteStoredZip(zipPath, entryName, "nope"u8.ToArray());
            Assert.Throws<InvalidDataException>(() => SafeZipExtractor.Validate(zipPath, new ZipSafetyLimits()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteStoredZip(string path, string entryName, byte[] data)
    {
        var name = Encoding.UTF8.GetBytes(entryName);
        var crc = Crc32(data);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x04034b50);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(crc);
        writer.Write(data.Length);
        writer.Write(data.Length);
        writer.Write((ushort)name.Length);
        writer.Write((ushort)0);
        writer.Write(name);
        writer.Write(data);

        var centralOffset = (uint)(30 + name.Length + data.Length);
        writer.Write(0x02014b50);
        writer.Write((ushort)20);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(crc);
        writer.Write(data.Length);
        writer.Write(data.Length);
        writer.Write((ushort)name.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(name);

        var centralSize = (uint)(46 + name.Length);
        writer.Write(0x06054b50);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(centralSize);
        writer.Write(centralOffset);
        writer.Write((ushort)0);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static List<ProdFalcon.Application.Scanning.Models.ScanIssue> SensitiveLog(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "Audit.cs");
        File.WriteAllText(file, source);
        try
        {
            return new SensitiveLoggingRule().EvaluateAsync(root, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static List<ProdFalcon.Application.Scanning.Models.ScanIssue> Http(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Client.cs"), source);
        try
        {
            return new HttpUsageRule().EvaluateAsync(root, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
