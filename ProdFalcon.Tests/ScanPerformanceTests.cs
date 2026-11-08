using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;

namespace ProdFalcon.Tests;

public class ScanPerformanceTests
{
    [Fact]
    public async Task Benchmark_rule_execution_on_a_medium_tree()
    {
        var root = Path.Combine(Path.GetTempPath(), "pf-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            WriteTree(root, sourceFiles: 400, noiseFiles: 200);
            var executor = new ScanRuleExecutor(AllRules(), NullLogger<ScanRuleExecutor>.Instance);

            var stopwatch = Stopwatch.StartNew();
            var summary = await executor.ExecuteAllAsync(root);
            stopwatch.Stop();

            var ruleMs = summary.Logs.Sum(log => log.DurationMs);
            Assert.True(summary.Logs.Count >= 15);
            Assert.Contains(summary.Logs, log => log.Succeeded);

            // Written for the audit log. The assertion above is the functional check.
            Console.WriteLine(
                $"BENCH rules={summary.Logs.Count} wallMs={stopwatch.ElapsedMilliseconds} ruleMsSum={ruleMs} issues={summary.Issues.Count} failed={summary.Logs.Count(log => !log.Succeeded)}");
            foreach (var log in summary.Logs.OrderByDescending(item => item.DurationMs).Take(8))
                Console.WriteLine($"BENCH rule {log.RuleName} {log.DurationMs}ms issues={log.IssuesFound} ok={log.Succeeded}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<IScanRule> AllRules() =>
    [
        new HardcodedConnectionStringRule(),
        new HardcodedJwtSecretRule(),
        new ApiKeyExposureRule(),
        new DebugModeRule(),
        new SqlInjectionRiskRule(),
        new HttpUsageRule(),
        new SensitiveLoggingRule(),
        new CorsWildcardRule(),
        new MissingAuthorizationRule(),
        new PlainTextPasswordRule(),
        new SwaggerInProductionRule(),
        new MissingLoggingRule(),
        new ConfigurationSecurityRule(),
        new CodeQualityRule(),
        new ExtendedStaticAnalysisRule(),
        new OwaspPatternRule()
    ];

    internal static void WriteTree(string root, int sourceFiles, int noiseFiles)
    {
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        var body = """
            using System;
            namespace Sample;
            public class Widget
            {
                public string Name { get; set; } = "ok";
                public void Run(string id)
                {
                    var value = id;
                    Console.WriteLine(value);
                }
            }
            """;
        for (var i = 0; i < sourceFiles; i++)
            File.WriteAllText(Path.Combine(src, $"File{i}.cs"), body + $"// file {i}{Environment.NewLine}");

        File.WriteAllText(Path.Combine(src, "UnsafeSql.cs"),
            "var sql = \"SELECT * FROM Users WHERE Name = \" + name;\n");

        var noise = Path.Combine(root, "node_modules", "leftpad");
        Directory.CreateDirectory(noise);
        for (var i = 0; i < noiseFiles; i++)
            File.WriteAllText(Path.Combine(noise, $"index{i}.js"), "module.exports = function(){ return 1; };\n");
    }
}
