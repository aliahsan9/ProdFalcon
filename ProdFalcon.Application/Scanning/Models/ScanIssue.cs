using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class ScanIssue : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int ScanResultId { get; set; }

    public ScanResult? ScanResult { get; set; }

    public int LineNumber { get; set; }

    /// <summary>1-based column of the match. Zero means the scanner did not record a column.</summary>
    public int ColumnNumber { get; set; }

    public string Description { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string FilePath { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    /// <summary>Stable identity for the same issue across scans: rule, path, line, and title.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public string Confidence { get; set; } = "High";

    public string? Cwe { get; set; }

    public string? Cve { get; set; }

    /// <summary>Redacted snippet. Full secret values are never stored.</summary>
    public string Evidence { get; set; } = string.Empty;

    public string Impact { get; set; } = string.Empty;

    public string Remediation { get; set; } = string.Empty;

    public string SecureExample { get; set; } = string.Empty;

    /// <summary>Open, Resolved, or AcceptedRisk.</summary>
    public string Status { get; set; } = "Open";

    public string DetectionMethod { get; set; } = "Static analysis";

    public string References { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
