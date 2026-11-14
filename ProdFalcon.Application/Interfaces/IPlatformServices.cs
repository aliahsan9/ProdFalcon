using ProdFalcon.Application.Scanning.GitHub;

namespace ProdFalcon.Application.Interfaces;

public interface IGitHubConnectionService
{
    Task<GitHubConnectionDto> GetStatusAsync(CancellationToken cancellationToken = default);

    string BuildAuthorizeUrl(string redirectUri);

    Task CompleteOAuthAsync(string code, string state, string redirectUri, CancellationToken cancellationToken = default);

    Task<GitHubConnectionDto> ConnectTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubRepositoryDto>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<QueuedRepositoryScanDto> ScanRepositoryAsync(string owner, string name, string? branch, CancellationToken cancellationToken = default);

    Task<PullRequestScanDto> QueuePullRequestScanAsync(string owner, string name, int number, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequestScanDto>> ListPullRequestsAsync(CancellationToken cancellationToken = default);

    Task RecordPullRequestAsync(PullRequestRecord record, CancellationToken cancellationToken = default);
}

public interface ISecurityPolicyService
{
    Task<SecurityPolicyDto> GetAsync(CancellationToken cancellationToken = default);

    Task<SecurityPolicyDto> UpdateAsync(SecurityPolicyDto request, CancellationToken cancellationToken = default);
}

public interface ICollaborationService
{
    Task<IReadOnlyList<CollaborationNoteDto>> ListAsync(int? scanResultId, CancellationToken cancellationToken = default);

    Task<CollaborationNoteDto> AddAsync(int? scanResultId, Guid? projectId, string body, CancellationToken cancellationToken = default);
}

public interface IAnalyticsService
{
    Task<AnalyticsOverviewDto> GetOverviewAsync(int days, CancellationToken cancellationToken = default);
}
