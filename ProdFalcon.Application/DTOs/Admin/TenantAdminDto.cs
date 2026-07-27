using ProdFalcon.Shared.Enums;

namespace ProdFalcon.Application.DTOs.Admin;

public class TenantAdminDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Plan { get; set; } = string.Empty;

    public long StorageUsed { get; set; }

    public long StorageLimit { get; set; }

    public int ScanLimit { get; set; }

    public int AIUsage { get; set; }

    public int MemberCount { get; set; }

    public int ProjectCount { get; set; }

    public int ScanCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? OwnerUserId { get; set; }
}
