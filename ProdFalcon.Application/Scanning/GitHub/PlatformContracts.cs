namespace ProdFalcon.Application.Scanning.GitHub;

public sealed class GitHubConnectionDto
{
    public bool Connected { get; set; }

    public string? Login { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Scopes { get; set; }

    public DateTime? ConnectedAt { get; set; }

    public bool OAuthConfigured { get; set; }
}

public sealed class GitHubRepositoryDto
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DefaultBranch { get; set; } = "main";

    public bool Private { get; set; }

    public string? Language { get; set; }

    public string HtmlUrl { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }
}

public sealed class QueuedRepositoryScanDto
{
    public Guid ProjectId { get; set; }

    public int ScanResultId { get; set; }
}

public sealed class PullRequestScanDto
{
    public int Id { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Branch { get; set; }

    public string? HeadSha { get; set; }

    public string Status { get; set; } = string.Empty;

    public int? ScanResultId { get; set; }

    public string? Summary { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool GatePassed { get; set; }
}

public sealed class PullRequestRecord
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? HeadSha { get; set; }

    public string? Branch { get; set; }

    public string Status { get; set; } = "Queued";

    public int? ScanResultId { get; set; }

    public string? Summary { get; set; }

    public string Source { get; set; } = "Webhook";
}

public sealed class SecurityPolicyDto
{
    public bool Enabled { get; set; } = true;

    public bool FailOnCritical { get; set; } = true;

    public bool FailOnHigh { get; set; }

    public int MaxOpenCritical { get; set; }

    public int MaxOpenHigh { get; set; } = 25;

    public int MinOverallScore { get; set; }

    public int MinSecurityScore { get; set; }

    public bool PreventScoreRegression { get; set; }

    public bool FailOnNewSecrets { get; set; } = true;

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<string> Rules { get; set; } = [];

    public QualityGateSettings ToSettings() => new(
        Enabled,
        FailOnCritical,
        FailOnHigh,
        MaxOpenCritical,
        MaxOpenHigh,
        MinOverallScore,
        MinSecurityScore,
        PreventScoreRegression,
        FailOnNewSecrets);
}

public sealed class CollaborationNoteDto
{
    public int Id { get; set; }

    public int? ScanResultId { get; set; }

    public Guid? ProjectId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public sealed class AnalyticsPointDto
{
    public DateTime Day { get; set; }

    public int Scans { get; set; }

    public double AverageScore { get; set; }

    public int OpenCritical { get; set; }

    public int OpenHigh { get; set; }
}

public sealed class ProjectRiskDto
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? RepositoryUrl { get; set; }

    public int LatestScore { get; set; }

    public int SecurityScore { get; set; }

    public int OpenCritical { get; set; }

    public int OpenHigh { get; set; }

    public string Trend { get; set; } = "Stable";

    public int? LatestScanResultId { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class AnalyticsOverviewDto
{
    public int Days { get; set; }

    public int ScanCount { get; set; }

    public double AverageScore { get; set; }

    public int ScoreChange { get; set; }

    public int OpenCritical { get; set; }

    public int OpenHigh { get; set; }

    public int ResolvedIssues { get; set; }

    public IReadOnlyList<AnalyticsPointDto> Points { get; set; } = [];

    public IReadOnlyList<ProjectRiskDto> Projects { get; set; } = [];

    public IReadOnlyList<PullRequestScanDto> RecentGates { get; set; } = [];
}
