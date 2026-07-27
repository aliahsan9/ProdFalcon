using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class ScanProject : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Processing";

    public string ZipPath { get; set; } = string.Empty;

    public string ExtractedPath { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public List<ScanResult> Results { get; set; } = [];
}
