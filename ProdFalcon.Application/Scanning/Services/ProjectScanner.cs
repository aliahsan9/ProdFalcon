using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Services;

public class ProjectScanner : IProjectScanner
{
    private readonly IScanRuleExecutor _ruleExecutor;
    private readonly IRiskScoringService _riskScoring;

    public ProjectScanner(IScanRuleExecutor ruleExecutor, IRiskScoringService riskScoring)
    {
        _ruleExecutor = ruleExecutor;
        _riskScoring = riskScoring;
    }

    public async Task<ScanResult> ScanAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path cannot be empty");

        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"Project path not found: {projectPath}");

        var execution = await _ruleExecutor.ExecuteAllAsync(projectPath, cancellationToken);
        var scores = _riskScoring.Calculate(execution.Issues);

        return new ScanResult
        {
            ProjectPath = projectPath,
            CreatedAt = DateTime.UtcNow,
            Issues = execution.Issues,
            Score = scores.OverallScore,
            SecurityScore = scores.SecurityScore,
            MaintainabilityScore = scores.MaintainabilityScore,
            PerformanceScore = scores.PerformanceScore,
            ProductionReadinessScore = scores.ProductionReadinessScore
        };
    }
}
