using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Services;

public class ScanService : IScanService
{
    private readonly IScanResultRepository _scanResultRepository;
    private readonly IEnumerable<IScanRule> _rules;

    public ScanService(
        IScanResultRepository scanResultRepository,
        IEnumerable<IScanRule> rules)
    {
        _scanResultRepository = scanResultRepository;
        _rules = rules;
    }

    public async Task<ScanResultDto> ScanProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        var result = new ScanResult
        {
            ProjectPath = projectPath,
            CreatedAt = DateTime.UtcNow,
            Issues = new List<ScanIssue>()
        };

        foreach (var rule in _rules)
        {
            var issues = await rule.EvaluateAsync(projectPath, cancellationToken);
            result.Issues.AddRange(issues);
        }

        // Save using abstraction (NOT DbContext)
        await _scanResultRepository.SaveAsync(result, cancellationToken);

        return new ScanResultDto
        {
            SessionId = result.Id,
            TotalIssues = result.Issues.Count,
            ProjectPath = result.ProjectPath
        };
    }
}