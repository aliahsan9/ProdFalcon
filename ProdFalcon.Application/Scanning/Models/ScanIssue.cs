using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class ScanIssue : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int ScanResultId { get; set; }

    public ScanResult? ScanResult { get; set; }

    public int LineNumber { get; set; }

    public string Description { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string FilePath { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Category { get; set; } = "General";
}
