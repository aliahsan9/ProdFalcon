using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IScanRuleExecutor
{
    Task<RuleExecutionSummary> ExecuteAllAsync(string projectPath, CancellationToken cancellationToken = default);
}

public class RuleExecutionSummary
{
    public List<ScanIssue> Issues { get; set; } = [];
    public List<RuleExecutionLog> Logs { get; set; } = [];
}

public class RuleExecutionLog
{
    public string RuleName { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public int IssuesFound { get; set; }
}
