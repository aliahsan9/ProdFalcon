using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class ScanResult : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ScanProjectId { get; set; }

    public ScanProject? ScanProject { get; set; }

    public string ProjectPath { get; set; } = string.Empty;

    public int Score { get; set; }

    public int SecurityScore { get; set; }

    public int MaintainabilityScore { get; set; }

    public int PerformanceScore { get; set; }

    public int ProductionReadinessScore { get; set; }

    public string Status { get; set; } = "Completed";

    public int DurationMs { get; set; }

    public List<ScanIssue> Issues { get; set; } = [];
}
