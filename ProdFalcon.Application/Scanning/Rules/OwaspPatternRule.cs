using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class OwaspPatternRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.StaticAnalysis;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(OwaspPatternAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
