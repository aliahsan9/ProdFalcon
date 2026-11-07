using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class TrackedFinding : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ScanProjectId { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string Status { get; set; } = "Detected";

    public int OccurrenceCount { get; set; }

    public int FirstScanResultId { get; set; }

    public int LastScanResultId { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? Notes { get; set; }
}
