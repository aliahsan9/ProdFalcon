using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class CopilotMessage : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int ScanResultId { get; set; }

    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
