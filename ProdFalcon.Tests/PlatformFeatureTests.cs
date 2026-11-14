using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ProdFalcon.Application.DTOs.Auth;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Shared.Responses;
using Xunit;

namespace ProdFalcon.Tests;

public class PlatformFeatureTests : IClassFixture<ProdFalconWebApplicationFactory>
{
    private readonly ProdFalconWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PlatformFeatureTests(ProdFalconWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabase();
    }

    [Fact]
    public async Task Owner_can_save_a_quality_gate_and_a_viewer_cannot()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await RegisterAsync(ownerClient, $"owner-{Guid.NewGuid():N}@test.com", "Owner");
        Authenticate(ownerClient, owner.Token);

        var initial = await ownerClient.GetFromJsonAsync<ApiResponse<SecurityPolicyDto>>("/api/policies", JsonOptions);
        Assert.NotNull(initial?.Data);
        Assert.True(initial.Data.FailOnCritical);
        Assert.Contains(initial.Data.Rules, rule => rule.Contains("critical", StringComparison.OrdinalIgnoreCase));

        initial.Data.MinSecurityScore = 75;
        initial.Data.PreventScoreRegression = true;
        var saved = await ownerClient.PutAsJsonAsync("/api/policies", initial.Data);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var inviteResponse = await ownerClient.PostAsJsonAsync("/api/tenants/members/invite", new
        {
            email = $"viewer-{Guid.NewGuid():N}@test.com",
            fullName = "Viewer",
            role = 0
        });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<ApiResponse<InvitePayload>>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(invite?.Data?.InviteToken));

        var acceptClient = _factory.CreateClient();
        var accept = await acceptClient.PostAsJsonAsync("/api/tenants/invites/accept", new
        {
            token = invite!.Data!.InviteToken,
            password = "Password123!",
            fullName = "Viewer"
        });
        accept.EnsureSuccessStatusCode();
        var accepted = await accept.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.Equal("Viewer", accepted?.Data?.Role);

        Authenticate(acceptClient, accepted!.Data!.Token);
        var denied = await acceptClient.PutAsJsonAsync("/api/policies", initial.Data);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Analytics_notes_and_github_status_are_available_to_the_workspace()
    {
        var client = _factory.CreateClient();
        var owner = await RegisterAsync(client, $"analyst-{Guid.NewGuid():N}@test.com", "Analyst");
        Authenticate(client, owner.Token);

        var analytics = await client.GetFromJsonAsync<ApiResponse<AnalyticsOverviewDto>>("/api/analytics?days=30", JsonOptions);
        Assert.NotNull(analytics?.Data);
        Assert.Equal(30, analytics.Data.Days);
        Assert.Equal(0, analytics.Data.ScanCount);

        var connection = await client.GetFromJsonAsync<ApiResponse<GitHubConnectionDto>>("/api/github/connection", JsonOptions);
        Assert.NotNull(connection?.Data);
        Assert.False(connection.Data.Connected);

        var noteResponse = await client.PostAsJsonAsync("/api/collaboration/notes", new { body = "Reviewed the authentication flow." });
        noteResponse.EnsureSuccessStatusCode();
        var notes = await client.GetFromJsonAsync<ApiResponse<List<CollaborationNoteDto>>>("/api/collaboration/notes", JsonOptions);
        Assert.Contains(notes?.Data ?? [], note => note.Body.Contains("authentication"));
    }

    private static async Task<AuthResponseDto> RegisterAsync(HttpClient client, string email, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            fullName = name,
            password = "Password123!",
            organizationName = $"{name} Org"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(auth);
        return auth;
    }

    private static void Authenticate(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class InvitePayload
    {
        public string InviteToken { get; set; } = string.Empty;
    }
}
