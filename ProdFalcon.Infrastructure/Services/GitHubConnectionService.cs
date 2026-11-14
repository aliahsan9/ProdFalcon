using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Security;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class GitHubConnectionService : IGitHubConnectionService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IScanProjectRepository _projects;
    private readonly IScanResultRepository _results;
    private readonly IScanQueue _queue;
    private readonly ISecurityPolicyService _policies;
    private readonly IDataProtector _tokens;
    private readonly IDataProtector _state;

    public GitHubConnectionService(
        ApplicationDbContext db,
        ITenantProvider tenant,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IScanProjectRepository projects,
        IScanResultRepository results,
        IScanQueue queue,
        ISecurityPolicyService policies,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _projects = projects;
        _results = results;
        _queue = queue;
        _policies = policies;
        _tokens = dataProtection.CreateProtector("ProdFalcon.GitHub.Token.v1");
        _state = dataProtection.CreateProtector("ProdFalcon.GitHub.OAuthState.v1");
    }

    public async Task<GitHubConnectionDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        var connection = await CurrentConnectionAsync(cancellationToken);
        return ToDto(connection);
    }

    public string BuildAuthorizeUrl(string redirectUri)
    {
        RequireSignedIn();
        TenantAccess.Require(TenantAccess.CanScan(_tenant.Role, _tenant.IsSuperAdmin), "Viewers cannot connect GitHub.");

        var clientId = _configuration["GitHub:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GitHub OAuth is not configured. Add GitHub:ClientId and GitHub:ClientSecret, or connect a personal access token.");

        if (_tenant.UserId is null)
            throw new UnauthorizedAccessException("Not authenticated.");

        var payload = $"{_tenant.TenantId:N}|{_tenant.UserId.Value}|{DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}";
        var state = Uri.EscapeDataString(_state.Protect(payload));
        var redirect = Uri.EscapeDataString(redirectUri);
        var scope = Uri.EscapeDataString("read:user repo");
        return $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={redirect}&scope={scope}&state={state}";
    }

    public async Task CompleteOAuthAsync(string code, string state, string redirectUri, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = ReadState(state);
        _tenant.SetTenant(tenantId, userId);

        var token = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        var profile = await ReadUserAsync(token.AccessToken, cancellationToken);
        await SaveConnectionAsync(userId, profile, token.AccessToken, token.Scope, cancellationToken);
    }

    public async Task<GitHubConnectionDto> ConnectTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        TenantAccess.Require(TenantAccess.CanScan(_tenant.Role, _tenant.IsSuperAdmin), "Viewers cannot connect GitHub.");

        var token = accessToken.Trim();
        if (token.Length < 20)
            throw new InvalidOperationException("Enter a GitHub personal access token with repository read access.");

        var profile = await ReadUserAsync(token, cancellationToken);
        var saved = await SaveConnectionAsync(_tenant.UserId!.Value, profile, token, "personal-access-token", cancellationToken);
        return ToDto(saved);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        TenantAccess.Require(TenantAccess.CanScan(_tenant.Role, _tenant.IsSuperAdmin), "Viewers cannot disconnect GitHub.");

        var connection = await _db.GitHubAccountConnections
            .FirstOrDefaultAsync(item => item.UserId == _tenant.UserId, cancellationToken);
        if (connection == null)
            return;

        _db.GitHubAccountConnections.Remove(connection);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubRepositoryDto>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        using var document = await GetJsonAsync(
            "https://api.github.com/user/repos?per_page=50&sort=updated&affiliation=owner,collaborator,organization_member",
            token,
            cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var repos = new List<GitHubRepositoryDto>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var owner = item.TryGetProperty("owner", out var ownerElement) && ownerElement.TryGetProperty("login", out var login)
                ? login.GetString()
                : null;
            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
                continue;

            repos.Add(new GitHubRepositoryDto
            {
                Owner = owner,
                Name = name,
                Description = item.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                    ? description.GetString()
                    : null,
                DefaultBranch = item.TryGetProperty("default_branch", out var branch) ? branch.GetString() ?? "main" : "main",
                Private = item.TryGetProperty("private", out var isPrivate) && isPrivate.GetBoolean(),
                Language = item.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String
                    ? language.GetString()
                    : null,
                HtmlUrl = item.TryGetProperty("html_url", out var url) ? url.GetString() ?? string.Empty : string.Empty,
                UpdatedAt = item.TryGetProperty("updated_at", out var updated) && updated.TryGetDateTime(out var when)
                    ? when
                    : null
            });
        }

        return repos;
    }

    public async Task<QueuedRepositoryScanDto> ScanRepositoryAsync(string owner, string name, string? branch, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        ValidateRepositoryName(owner, name);
        if (!string.IsNullOrWhiteSpace(branch) && !GitHubUrlParser.IsValidBranch(branch))
            throw new InvalidOperationException("The branch name is not valid.");

        var project = new ScanProject
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            FileName = $"{owner}/{name}",
            DisplayName = $"{owner}/{name}",
            UploadedAt = DateTime.UtcNow,
            Status = "Queued",
            SourceType = "GitHub",
            RepositoryUrl = $"https://github.com/{owner}/{name}",
            Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim(),
            UserId = _tenant.UserId
        };
        await _projects.CreateAsync(project, cancellationToken);
        var scanId = await QueueScanAsync(project, token, "Queued from a connected GitHub account.", cancellationToken);
        return new QueuedRepositoryScanDto { ProjectId = project.Id, ScanResultId = scanId };
    }

    public async Task<PullRequestScanDto> QueuePullRequestScanAsync(string owner, string name, int number, CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken);
        ValidateRepositoryName(owner, name);
        if (number <= 0)
            throw new InvalidOperationException("Enter a pull request number.");

        using var document = await GetJsonAsync(
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/pulls/{number}",
            token,
            cancellationToken);

        var root = document.RootElement;
        var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? $"Pull request {number}" : $"Pull request {number}";
        string? headBranch = null;
        string? sha = null;
        var scanOwner = owner;
        var scanName = name;
        if (root.TryGetProperty("head", out var head))
        {
            if (head.TryGetProperty("ref", out var refElement))
                headBranch = refElement.GetString();
            if (head.TryGetProperty("sha", out var shaElement))
                sha = shaElement.GetString();
            if (head.TryGetProperty("repo", out var repo) && repo.ValueKind == JsonValueKind.Object)
            {
                var fullName = repo.TryGetProperty("full_name", out var fullNameElement) ? fullNameElement.GetString() : null;
                var parts = fullName?.Split('/', 2);
                if (parts is { Length: 2 })
                {
                    scanOwner = parts[0];
                    scanName = parts[1];
                }
            }
        }

        var project = new ScanProject
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            FileName = $"{owner}/{name}#{number}",
            DisplayName = $"{owner}/{name}#{number}",
            UploadedAt = DateTime.UtcNow,
            Status = "Queued",
            SourceType = "GitHub",
            RepositoryUrl = $"https://github.com/{scanOwner}/{scanName}",
            Branch = headBranch,
            CommitSha = sha,
            UserId = _tenant.UserId
        };
        await _projects.CreateAsync(project, cancellationToken);
        var scanId = await QueueScanAsync(project, token, $"Queued from pull request #{number}.", cancellationToken);

        var row = new PullRequestScan
        {
            TenantId = _tenant.TenantId,
            Owner = owner,
            Name = name,
            Number = number,
            Title = Trim(title, 300),
            HeadSha = sha,
            Branch = headBranch,
            Status = "Queued",
            ScanResultId = scanId,
            Summary = "Scan queued. The quality gate is applied when the scan finishes.",
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        };
        _db.PullRequestScans.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(row, null);
    }

    public async Task<IReadOnlyList<PullRequestScanDto>> ListPullRequestsAsync(CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        var rows = await _db.PullRequestScans
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        var scanIds = rows.Where(item => item.ScanResultId.HasValue).Select(item => item.ScanResultId!.Value).Distinct().ToList();
        var scans = scanIds.Count == 0
            ? []
            : await _db.ScanResults.AsNoTracking().Where(item => scanIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var issues = scanIds.Count == 0
            ? []
            : await _db.ScanIssues.AsNoTracking()
                .Where(item => scanIds.Contains(item.ScanResultId))
                .Select(item => new IssueFact(item.ScanResultId, item.Severity, item.Status, item.Title, item.RuleName, item.Category))
                .ToListAsync(cancellationToken);

        var policy = await _policies.GetAsync(cancellationToken);
        return rows.Select(row => ToDto(row, EvaluateLinkedScan(row, scans, issues, policy))).ToList();
    }

    public async Task RecordPullRequestAsync(PullRequestRecord record, CancellationToken cancellationToken = default)
    {
        if (_tenant.TenantId == Guid.Empty || record.Number <= 0)
            return;

        _db.PullRequestScans.Add(new PullRequestScan
        {
            TenantId = _tenant.TenantId,
            Owner = Trim(record.Owner, 100),
            Name = Trim(record.Name, 100),
            Number = record.Number,
            Title = Trim(string.IsNullOrWhiteSpace(record.Title) ? $"Pull request {record.Number}" : record.Title, 300),
            HeadSha = record.HeadSha,
            Branch = record.Branch,
            Status = string.IsNullOrWhiteSpace(record.Status) ? "Queued" : record.Status,
            ScanResultId = record.ScanResultId,
            Summary = string.IsNullOrWhiteSpace(record.Summary) ? null : Trim(record.Summary, 2000),
            Source = string.IsNullOrWhiteSpace(record.Source) ? "Webhook" : record.Source,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> QueueScanAsync(ScanProject project, string token, string message, CancellationToken cancellationToken)
    {
        var saved = await _results.SaveAsync(new ScanResult
        {
            TenantId = project.TenantId,
            ScanProjectId = project.Id,
            CreatedAt = DateTime.UtcNow,
            Status = "Queued",
            Stage = "Queued",
            StageMessage = message,
            CommitSha = project.CommitSha
        }, cancellationToken);

        await _queue.QueueAsync(new ScanWorkItem
        {
            TenantId = _tenant.TenantId,
            UserId = _tenant.UserId,
            ProjectId = project.Id,
            ScanResultId = saved.Id,
            GitHubAccessToken = token
        }, cancellationToken);

        return saved.Id;
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken)
    {
        RequireSignedIn();
        TenantAccess.Require(TenantAccess.CanScan(_tenant.Role, _tenant.IsSuperAdmin), "Viewers cannot start scans.");
        var connection = await CurrentConnectionAsync(cancellationToken);
        if (connection == null)
            throw new InvalidOperationException("Connect GitHub before scanning a repository or pull request.");

        try
        {
            return _tokens.Unprotect(connection.ProtectedAccessToken);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            throw new InvalidOperationException("The saved GitHub connection can no longer be read. Connect GitHub again.");
        }
    }

    private async Task<GitHubAccountConnection?> CurrentConnectionAsync(CancellationToken cancellationToken) =>
        await _db.GitHubAccountConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == _tenant.UserId, cancellationToken);

    private async Task<GitHubAccountConnection> SaveConnectionAsync(
        int userId,
        GitHubProfile profile,
        string accessToken,
        string? scopes,
        CancellationToken cancellationToken)
    {
        var existing = await _db.GitHubAccountConnections
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        var connection = existing ?? new GitHubAccountConnection { UserId = userId, TenantId = _tenant.TenantId };
        connection.GitHubLogin = profile.Login;
        connection.GitHubUserId = profile.Id;
        connection.AvatarUrl = profile.AvatarUrl;
        connection.Scopes = Trim(scopes, 400) ?? string.Empty;
        connection.ProtectedAccessToken = _tokens.Protect(accessToken);
        connection.ConnectedAt = DateTime.UtcNow;

        if (existing == null)
            _db.GitHubAccountConnections.Add(connection);

        await _db.SaveChangesAsync(cancellationToken);
        return connection;
    }

    private (Guid TenantId, int UserId) ReadState(string state)
    {
        string payload;
        try
        {
            payload = _state.Unprotect(Uri.UnescapeDataString(state));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            throw new UnauthorizedAccessException("The GitHub sign-in session expired. Start the connection again.");
        }

        var parts = payload.Split('|');
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out var tenantId)
            || !int.TryParse(parts[1], out var userId)
            || !long.TryParse(parts[2], out var expiry)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
            throw new UnauthorizedAccessException("The GitHub sign-in session expired. Start the connection again.");

        return (tenantId, userId);
    }

    private async Task<(string AccessToken, string? Scope)> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var clientId = _configuration["GitHub:ClientId"];
        var clientSecret = _configuration["GitHub:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("GitHub OAuth is not configured.");

        var client = _httpClientFactory.CreateClient("GitHub");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("ProdFalcon");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("GitHub did not accept the sign-in code.");

        using var document = JsonDocument.Parse(body);
        var token = document.RootElement.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub did not return an access token.");

        var scope = document.RootElement.TryGetProperty("scope", out var scopeElement) ? scopeElement.GetString() : null;
        return (token, scope);
    }

    private async Task<GitHubProfile> ReadUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync("https://api.github.com/user", accessToken, cancellationToken);
        var root = document.RootElement;
        var login = root.TryGetProperty("login", out var loginElement) ? loginElement.GetString() : null;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetInt64() : 0;
        if (string.IsNullOrWhiteSpace(login) || id == 0)
            throw new InvalidOperationException("GitHub did not return a user profile for that token.");

        return new GitHubProfile(
            login,
            id,
            root.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("ProdFalcon");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("GitHub rejected the token. Connect again with a token that can read repositories.");
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("GitHub could not find that repository or pull request.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub returned {(int)response.StatusCode}.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static PullRequestScanDto ToDto(PullRequestScan row, GateSnapshot? gate) => new()
    {
        Id = row.Id,
        Owner = row.Owner,
        Name = row.Name,
        Number = row.Number,
        Title = row.Title,
        Branch = row.Branch,
        HeadSha = row.HeadSha,
        Status = gate?.Status ?? row.Status,
        ScanResultId = row.ScanResultId,
        Summary = gate?.Summary ?? row.Summary,
        Source = row.Source,
        CreatedAt = row.CreatedAt,
        GatePassed = gate?.Passed ?? string.Equals(row.Status, "Passed", StringComparison.OrdinalIgnoreCase)
    };

    private static GateSnapshot? EvaluateLinkedScan(
        PullRequestScan row,
        IReadOnlyList<ScanResult> scans,
        IReadOnlyList<IssueFact> issues,
        SecurityPolicyDto policy)
    {
        if (row.ScanResultId is null)
            return null;

        var scan = scans.FirstOrDefault(item => item.Id == row.ScanResultId.Value);
        if (scan == null)
            return null;

        if (!string.Equals(scan.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return new GateSnapshot(scan.Status, scan.StageMessage, false);

        var related = issues.Where(item => item.ScanResultId == scan.Id).ToList();
        var open = related.Where(item => QualityGateEvaluator.IsOpen(item.Status)).ToList();
        var result = QualityGateEvaluator.Evaluate(policy.ToSettings(), new QualityGateFacts(
            scan.Score,
            scan.SecurityScore,
            scan.PreviousScore,
            open.Count(item => IsSeverity(item.Severity, "Critical")),
            open.Count(item => IsSeverity(item.Severity, "High")),
            0,
            0,
            0));

        return new GateSnapshot(result.Passed ? "Passed" : "Failed", string.Join(" ", result.Messages), result.Passed);
    }

    private GitHubConnectionDto ToDto(GitHubAccountConnection? connection) => new()
    {
        Connected = connection != null,
        Login = connection?.GitHubLogin,
        AvatarUrl = connection?.AvatarUrl,
        Scopes = connection?.Scopes,
        ConnectedAt = connection?.ConnectedAt,
        OAuthConfigured = !string.IsNullOrWhiteSpace(_configuration["GitHub:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["GitHub:ClientSecret"])
    };

    private void RequireSignedIn()
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty || _tenant.UserId is null)
            throw new UnauthorizedAccessException("Not authenticated.");
    }

    private static void ValidateRepositoryName(string owner, string name)
    {
        if (!IsGitHubName(owner) || !IsGitHubName(name))
            throw new InvalidOperationException("Repository must be owner/name using letters, numbers, dots, dashes, or underscores.");
    }

    private static bool IsGitHubName(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100
        && value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool IsSeverity(string severity, string expected) =>
        string.Equals(severity, expected, StringComparison.OrdinalIgnoreCase);

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private sealed record GitHubProfile(string Login, long Id, string? AvatarUrl);

    private sealed record IssueFact(int ScanResultId, string Severity, string? Status, string Title, string RuleName, string Category);

    private sealed record GateSnapshot(string Status, string? Summary, bool Passed);
}
