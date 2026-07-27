using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Domain.Entities;

public class AuditLog : ITenantEntity
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? Metadata { get; set; }
}
