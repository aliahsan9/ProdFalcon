using System.Diagnostics;
using Microsoft.Extensions.Logging;
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

        foreach (var rule in _rules)
        {
            var ruleName = rule.GetType().Name;
            if (rule is Rules.IScanModule module && modules != null && !modules.IsEnabled(module.ModuleKey))
            {
                summary.Logs.Add(new RuleExecutionLog
                {
                    RuleName = ruleName,
                    Succeeded = true,
                    Error = "Skipped by scan configuration"
                });
                continue;
            }

            if (onRuleStarted != null)
                await onRuleStarted(ruleName, cancellationToken);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var issues = await rule.EvaluateAsync(projectPath, cancellationToken);
                stopwatch.Stop();

                foreach (var issue in issues)
                {
                    issue.RuleId = string.IsNullOrWhiteSpace(issue.RuleId) ? ruleName : issue.RuleId;
                    issue.RuleName = string.IsNullOrWhiteSpace(issue.RuleName) ? ruleName : issue.RuleName;
                    if (string.IsNullOrWhiteSpace(issue.Category) || issue.Category == "General")
                        issue.Category = ResolveCategory(ruleName);
                }

                if (issues.Count > 0)
                    summary.Issues.AddRange(issues);

                summary.Logs.Add(new RuleExecutionLog
                {
                    RuleName = ruleName,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    Succeeded = true,
                    IssuesFound = issues.Count
                });

                _logger.LogInformation(
                    "Rule {RuleName} completed in {DurationMs}ms with {IssueCount} issues",
                    ruleName, stopwatch.ElapsedMilliseconds, issues.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                summary.Logs.Add(new RuleExecutionLog
                {
                    RuleName = ruleName,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    Succeeded = false,
                    Error = ex.Message
                });

                _logger.LogWarning(ex, "Rule {RuleName} failed but scan continues", ruleName);
            }
        }

        return summary;
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
            nameof(Rules.HttpUsageRule) => "Performance",
            nameof(Rules.MissingLoggingRule) or
            nameof(Rules.SensitiveLoggingRule) or
            nameof(Rules.CodeQualityRule) => "Quality",
            _ => "Security"
        };
}
