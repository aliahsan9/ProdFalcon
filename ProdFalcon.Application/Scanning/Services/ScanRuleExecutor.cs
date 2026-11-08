using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Services;

public class ScanRuleExecutor : IScanRuleExecutor
{
    private readonly IEnumerable<IScanRule> _rules;
    private readonly ILogger<ScanRuleExecutor> _logger;

    public ScanRuleExecutor(IEnumerable<IScanRule> rules, ILogger<ScanRuleExecutor> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<RuleExecutionSummary> ExecuteAllAsync(
        string projectPath,
        CancellationToken cancellationToken = default,
        ScanModuleSelection? modules = null,
        Func<string, CancellationToken, Task>? onRuleStarted = null)
    {
        var summary = new RuleExecutionSummary();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return summary;

        var ownedScope = SourceScanScope.Current == null;
        var snapshot = SourceScanScope.Current;
        IDisposable? scope = null;
        if (ownedScope)
        {
            snapshot = await SourceSnapshotLoader.LoadAsync(projectPath, new ScanLimitsOptions(), cancellationToken);
            scope = SourceScanScope.Push(snapshot);
        }

        try
        {
            if (snapshot != null)
            {
                summary.Logs.Add(new RuleExecutionLog
                {
                    RuleName = "SourceSnapshot",
                    DurationMs = snapshot.EnumerationMs + snapshot.ReadMs,
                    Succeeded = true,
                    IssuesFound = snapshot.Files.Count,
                    Error = snapshot.Truncated
                        ? $"File list truncated. Enumeration {snapshot.EnumerationMs}ms, read {snapshot.ReadMs}ms, skipped directories {snapshot.SkippedDirectories}."
                        : $"Enumeration {snapshot.EnumerationMs}ms, read {snapshot.ReadMs}ms, skipped directories {snapshot.SkippedDirectories}, skipped large {snapshot.SkippedLarge}, skipped binary {snapshot.SkippedBinary}."
                });
                _logger.LogInformation(
                    "Loaded {FileCount} source files in {EnumerationMs}ms enumeration and {ReadMs}ms read. Skipped directories {SkippedDirectories}, large {SkippedLarge}, binary {SkippedBinary}, truncated {Truncated}",
                    snapshot.Files.Count,
                    snapshot.EnumerationMs,
                    snapshot.ReadMs,
                    snapshot.SkippedDirectories,
                    snapshot.SkippedLarge,
                    snapshot.SkippedBinary,
                    snapshot.Truncated);
            }

            var degree = Math.Clamp(snapshot?.MaxDegreeOfParallelism ?? 4, 1, 8);
            var progress = new SemaphoreSlim(1, 1);
            var issues = new ConcurrentBag<ScanIssue>();
            var logs = new ConcurrentBag<RuleExecutionLog>();

            await Parallel.ForEachAsync(_rules, new ParallelOptions
            {
                MaxDegreeOfParallelism = degree,
                CancellationToken = cancellationToken
            }, async (rule, token) =>
            {
                var ruleName = rule.GetType().Name;
                if (rule is IScanModule module && modules != null && !modules.IsEnabled(module.ModuleKey))
                {
                    logs.Add(new RuleExecutionLog
                    {
                        RuleName = ruleName,
                        Succeeded = true,
                        Error = "Skipped by scan configuration"
                    });
                    return;
                }

                if (onRuleStarted != null)
                {
                    await progress.WaitAsync(token);
                    try
                    {
                        await onRuleStarted(ruleName, token);
                    }
                    finally
                    {
                        progress.Release();
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var found = await rule.EvaluateAsync(projectPath, token);
                    stopwatch.Stop();
                    foreach (var issue in found)
                    {
                        issue.RuleId = string.IsNullOrWhiteSpace(issue.RuleId) ? ruleName : issue.RuleId;
                        issue.RuleName = string.IsNullOrWhiteSpace(issue.RuleName) ? ruleName : issue.RuleName;
                        if (string.IsNullOrWhiteSpace(issue.Category) || issue.Category == "General")
                            issue.Category = ResolveCategory(ruleName);

                        issues.Add(issue);
                    }

                    logs.Add(new RuleExecutionLog
                    {
                        RuleName = ruleName,
                        DurationMs = (int)stopwatch.ElapsedMilliseconds,
                        Succeeded = true,
                        IssuesFound = found.Count
                    });
                    _logger.LogInformation(
                        "Rule {RuleName} completed in {DurationMs}ms with {IssueCount} issues",
                        ruleName, stopwatch.ElapsedMilliseconds, found.Count);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logs.Add(new RuleExecutionLog
                    {
                        RuleName = ruleName,
                        DurationMs = (int)stopwatch.ElapsedMilliseconds,
                        Succeeded = false,
                        Error = ex.Message
                    });
                    _logger.LogWarning(ex, "Rule {RuleName} failed but scan continues", ruleName);
                }
            });

            summary.Issues.AddRange(issues);
            summary.Logs.AddRange(logs.OrderBy(log => log.RuleName, StringComparer.Ordinal));
            return summary;
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static string ResolveCategory(string ruleName) =>
        ruleName switch
        {
            nameof(Rules.ApiKeyExposureRule) or
            nameof(Rules.HardcodedConnectionStringRule) or
            nameof(Rules.HardcodedJwtSecretRule) or
            nameof(Rules.PlainTextPasswordRule) => "Secrets",
            nameof(Rules.SqlInjectionRiskRule) => "Injection",
            nameof(Rules.MissingAuthorizationRule) => "Authentication",
            nameof(Rules.CorsWildcardRule) or
            nameof(Rules.SwaggerInProductionRule) or
            nameof(Rules.ConfigurationSecurityRule) or
            nameof(Rules.DebugModeRule) => "Configuration",
            nameof(Rules.HttpUsageRule) => "Transport",
            nameof(Rules.MissingLoggingRule) or
            nameof(Rules.SensitiveLoggingRule) or
            nameof(Rules.CodeQualityRule) => "Quality",
            _ => "Security"
        };
}
