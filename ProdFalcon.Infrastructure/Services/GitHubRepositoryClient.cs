using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Acquisition;

namespace ProdFalcon.Infrastructure.Services;

public class GitHubRepositoryClient : IRepositoryAcquisitionService
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.github.com",
        "codeload.github.com",
        "github.com",
        "objects.githubusercontent.com"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProjectStorageService _storage;
    private readonly ILogger<GitHubRepositoryClient> _logger;
    private readonly ScanLimitsOptions _limits;

    public GitHubRepositoryClient(
        IHttpClientFactory httpClientFactory,
        IProjectStorageService storage,
        IOptions<ScanLimitsOptions> limits,
        ILogger<GitHubRepositoryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _limits = limits.Value;
        _logger = logger;
    }

    public async Task<RepositoryAcquisitionResult> AcquireGitHubAsync(
        Guid projectId,
        GitHubRepositoryReference reference,
        string? branchOverride,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        var metadata = await GetMetadataAsync(client, reference, accessToken, cancellationToken);
        var branch = branchOverride ?? reference.Branch ?? metadata.DefaultBranch;
        if (!GitHubUrlParser.IsValidBranch(branch))
            throw new InvalidOperationException("The requested branch is not valid.");

        _logger.LogInformation(
            "Downloading GitHub repository {Owner}/{Name} branch {Branch}",
            reference.Owner, reference.Name, branch);

        var archiveUri = string.IsNullOrWhiteSpace(accessToken)
            ? GitHubUrlParser.BuildPublicArchiveUri(reference.Owner, reference.Name, branch)
            : new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(reference.Owner)}/{Uri.EscapeDataString(reference.Name)}/zipball/{Uri.EscapeDataString(branch)}");

        await using var archiveStream = await DownloadAsync(client, archiveUri, accessToken, cancellationToken);
        var zipPath = await _storage.SaveZipAsync(projectId, archiveStream, cancellationToken);
        var extracted = await _storage.ExtractZipAsync(projectId, zipPath, cancellationToken);

        return new RepositoryAcquisitionResult
        {
            ZipPath = zipPath,
            ExtractedPath = extracted,
            Branch = branch,
            CommitSha = await TryGetCommitShaAsync(client, reference, branch, accessToken, cancellationToken),
            Description = metadata.Description,
            Languages = metadata.Languages
        };
    }

    private async Task<RepoMetadata> GetMetadataAsync(
        HttpClient client,
        GitHubRepositoryReference reference,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(reference.Owner)}/{Uri.EscapeDataString(reference.Name)}");
        using var response = await SendAsync(client, uri, accessToken, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Repository was not found or is private. Public repositories do not need a token.");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub returned {(int)response.StatusCode} while reading repository metadata.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var defaultBranch = root.TryGetProperty("default_branch", out var branch) ? branch.GetString() : "main";
        var description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        var language = root.TryGetProperty("language", out var lang) && lang.ValueKind == JsonValueKind.String
            ? lang.GetString()
            : null;

        return new RepoMetadata(string.IsNullOrWhiteSpace(defaultBranch) ? "main" : defaultBranch!, description, language == null ? [] : [language]);
    }

    private async Task<Stream> DownloadAsync(HttpClient client, Uri uri, string? accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, uri, accessToken, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("The branch was not found on that repository.");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub returned {(int)response.StatusCode} while downloading the repository.");

        var length = response.Content.Headers.ContentLength;
        if (length > _limits.MaxUploadBytes)
            throw new InvalidOperationException("The repository archive exceeds the download size limit.");

        var memory = new MemoryStream();
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await network.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > _limits.MaxUploadBytes)
                throw new InvalidOperationException("The repository archive exceeds the download size limit.");

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        memory.Position = 0;
        return memory;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri uri, string? accessToken, CancellationToken cancellationToken)
    {
        var current = uri;
        for (var hop = 0; hop < 4; hop++)
        {
            EnsureAllowed(current);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("ProdFalcon");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
                return response;

            var next = response.Headers.Location;
            response.Dispose();
            if (next == null)
                throw new InvalidOperationException("GitHub returned a redirect without a location.");

            current = next.IsAbsoluteUri ? next : new Uri(current, next);
        }

        throw new InvalidOperationException("GitHub redirected too many times.");
    }

    private static void EnsureAllowed(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !AllowedHosts.Contains(uri.IdnHost))
            throw new InvalidOperationException("The download destination is not an allowed GitHub host.");
    }

    private async Task<string?> TryGetCommitShaAsync(
        HttpClient client,
        GitHubRepositoryReference reference,
        string branch,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(reference.Owner)}/{Uri.EscapeDataString(reference.Name)}/commits/{Uri.EscapeDataString(branch)}");
            using var response = await SendAsync(client, uri, accessToken, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("sha", out var sha) ? sha.GetString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            _logger.LogInformation(ex, "Commit SHA was not available for {Owner}/{Name}", reference.Owner, reference.Name);
            return null;
        }
    }

    private sealed record RepoMetadata(string DefaultBranch, string? Description, IReadOnlyList<string> Languages);
}
