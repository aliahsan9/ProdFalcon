using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public interface IScanRule
{
    Task<List<ScanIssue>> EvaluateAsync(string projectPath, CancellationToken cancellationToken);
}