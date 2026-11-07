using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Performance;
using ProdFalcon.Application.Scanning.Quality;

namespace ProdFalcon.Application.Scanning.Rules;

public class ExtendedStaticAnalysisRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.StaticAnalysis;

    public Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = CodeQualityAnalyzer.Analyze(projectPath);
        issues.AddRange(PerformanceReliabilityAnalyzer.Analyze(projectPath));
        return Task.FromResult(issues);
    }
}
