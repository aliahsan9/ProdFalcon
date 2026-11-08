using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;

var sourceFiles = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 400;
var noiseFiles = args.Length > 1 && int.TryParse(args[1], out var noise) ? noise : 200;
var root = Path.Combine(Path.GetTempPath(), "pf-bench-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
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

    var modules = Path.Combine(root, "node_modules", "leftpad");
    Directory.CreateDirectory(modules);
    for (var i = 0; i < noiseFiles; i++)
        File.WriteAllText(Path.Combine(modules, $"index{i}.js"), "module.exports = function(){ return 1; };\n");

    IScanRule[] rules =
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

    var executor = new ScanRuleExecutor(rules, NullLogger<ScanRuleExecutor>.Instance);
    var wall = Stopwatch.StartNew();
    var summary = await executor.ExecuteAllAsync(root);
    wall.Stop();

    Console.WriteLine($"BENCH files={sourceFiles} noise={noiseFiles} rules={summary.Logs.Count} wallMs={wall.ElapsedMilliseconds} ruleMsSum={summary.Logs.Sum(log => log.DurationMs)} issues={summary.Issues.Count} failed={summary.Logs.Count(log => !log.Succeeded)}");
    foreach (var log in summary.Logs.OrderByDescending(item => item.DurationMs))
        Console.WriteLine($"RULE {log.RuleName} {log.DurationMs}ms issues={log.IssuesFound} ok={log.Succeeded}");
}
finally
{
    Directory.Delete(root, recursive: true);
}
