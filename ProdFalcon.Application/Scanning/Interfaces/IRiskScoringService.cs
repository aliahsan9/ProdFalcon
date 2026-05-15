using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IRiskScoringService
{
    RiskScoreResult Calculate(IEnumerable<ScanIssue> issues);
}

public class RiskScoreResult
{
    public int OverallScore { get; set; }
    public int SecurityScore { get; set; }
    public int MaintainabilityScore { get; set; }
    public int PerformanceScore { get; set; }
    public int ProductionReadinessScore { get; set; }
}
