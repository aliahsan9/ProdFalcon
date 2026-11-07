namespace ProdFalcon.Application.Scanning.Acquisition;

public interface IRepositoryAcquisitionService
{
    Task<RepositoryAcquisitionResult> AcquireGitHubAsync(
        Guid projectId,
        GitHubRepositoryReference reference,
        string? branchOverride,
        string? accessToken,
        CancellationToken cancellationToken);
}

public sealed class RepositoryAcquisitionResult
{
    public string ZipPath { get; init; } = string.Empty;

    public string ExtractedPath { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string? CommitSha { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];
}
