using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class SqlInjectionRiskRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.SqlInjection;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".py" or ".js" or ".ts" or ".php" or ".java"))
                continue;

            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            issues.AddRange(SqlInjectionAnalyzer.AnalyzeFile(file, content));
        }

        return issues;
    }
}
