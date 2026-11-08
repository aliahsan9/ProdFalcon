using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class ApiKeyExposureRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Secrets;

    public async Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken)
    {
        var issues = new List<ScanIssue>();
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await SourceTree.TryReadTextAsync(file, cancellationToken);
            if (content == null)
                continue;

            foreach (var issue in SourceTree.SecretsFor(file, content))
            {
                if (issue.RuleId == "SEC-SECRET-001")
                    issues.Add(issue);
            }
        }

        return issues;
    }
}
