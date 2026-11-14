using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class RemediationProposal : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int ScanResultId { get; set; }

    public ScanResult? ScanResult { get; set; }

    public int ScanIssueId { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public int ColumnNumber { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public string SecureSuggestion { get; set; } = string.Empty;

    public string OriginalSnippet { get; set; } = string.Empty;

    public string PatchedSnippet { get; set; } = string.Empty;

    public string DiffText { get; set; } = string.Empty;

    /// <summary>Proposed, Approved, or Rejected.</summary>
    public string Status { get; set; } = "Proposed";

    public bool UsedExternalModel { get; set; }

    public string Grounding { get; set; } = string.Empty;

    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }
}
