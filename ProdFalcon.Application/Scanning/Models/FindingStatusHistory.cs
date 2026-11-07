using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class FindingStatusHistory : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int ScanIssueId { get; set; }

    public int? TrackedFindingId { get; set; }

    public string FromStatus { get; set; } = "Open";

    public string ToStatus { get; set; } = "Open";

    public string? Notes { get; set; }

    public int? UserId { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
