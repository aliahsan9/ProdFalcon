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

    /// <summary>Zip or GitHub.</summary>
    public string SourceType { get; set; } = "Zip";

    public string? RepositoryUrl { get; set; }

    public string? Branch { get; set; }

    public string? CommitSha { get; set; }

    public string? DisplayName { get; set; }

    public string DetectedTechnologies { get; set; } = string.Empty;

    public List<ScanResult> Results { get; set; } = [];
}
