using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class GitHubAccountConnection : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int UserId { get; set; }

    public string GitHubLogin { get; set; } = string.Empty;

    public long GitHubUserId { get; set; }

    public string ProtectedAccessToken { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string Scopes { get; set; } = string.Empty;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}

public class SecurityPolicy : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public bool Enabled { get; set; } = true;

    public bool FailOnCritical { get; set; } = true;

    public bool FailOnHigh { get; set; }

    public int MaxOpenCritical { get; set; }

    public int MaxOpenHigh { get; set; } = 25;

    public int MinOverallScore { get; set; }

    public int MinSecurityScore { get; set; }

    public bool PreventScoreRegression { get; set; }

    public bool FailOnNewSecrets { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PullRequestScan : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? HeadSha { get; set; }

    public string? Branch { get; set; }

    public string Status { get; set; } = "Queued";

    public int? ScanResultId { get; set; }

    public string? Summary { get; set; }

    public string Source { get; set; } = "Manual";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CollaborationNote : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int? ScanResultId { get; set; }

    public Guid? ProjectId { get; set; }

    public int AuthorUserId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
