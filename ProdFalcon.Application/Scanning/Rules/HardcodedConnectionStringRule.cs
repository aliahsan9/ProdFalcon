using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public class HardcodedConnectionStringRule : IScanRule, IScanModule
{
    public string ModuleKey => ScanModules.Secrets;

    public Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken) =>
        Task.FromResult(new List<ScanIssue>());
}
