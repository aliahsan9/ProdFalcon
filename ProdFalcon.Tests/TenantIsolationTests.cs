using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProdFalcon.Application.DTOs.Auth;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Shared.Responses;
using Xunit;

namespace ProdFalcon.Tests;

public class TenantIsolationTests : IClassFixture<ProdFalconWebApplicationFactory>
{
    private readonly ProdFalconWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TenantIsolationTests(ProdFalconWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabase();
    }

    [Fact]
    public async Task UserB_CannotAccess_UserA_ScanResult()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var authA = await RegisterAsync(clientA, "alice@test.com", "Alice");
        var authB = await RegisterAsync(clientB, "bob@test.com", "Bob");

        Assert.NotEqual(authA.TenantId, authB.TenantId);

        Authenticate(clientA, authA.Token);
        Authenticate(clientB, authB.Token);

        var scanResultId = await SeedScanForTenantAsync(authA.TenantId, authA.Email);

        var responseB = await clientB.GetAsync($"/api/scan/{scanResultId}");
        Assert.Equal(HttpStatusCode.NotFound, responseB.StatusCode);

        var responseA = await clientA.GetAsync($"/api/scan/{scanResultId}");
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
    }

    [Fact]
    public async Task UserB_Dashboard_DoesNotInclude_UserA_Projects()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var authA = await RegisterAsync(clientA, "alice2@test.com", "Alice2");
        var authB = await RegisterAsync(clientB, "bob2@test.com", "Bob2");

        Authenticate(clientA, authA.Token);
        Authenticate(clientB, authB.Token);

        await SeedScanForTenantAsync(authA.TenantId, authA.Email);

        var projectsB = await clientB.GetFromJsonAsync<ApiResponse<List<ProjectDashboardDto>>>(
            "/api/projects", JsonOptions);

        Assert.NotNull(projectsB);
        Assert.True(projectsB.Success);
        Assert.Empty(projectsB.Data ?? []);

        var projectsA = await clientA.GetFromJsonAsync<ApiResponse<List<ProjectDashboardDto>>>(
            "/api/projects", JsonOptions);

        Assert.NotNull(projectsA);
        Assert.NotEmpty(projectsA.Data ?? []);
    }

    [Fact]
    public async Task UserB_CannotRequest_AiSuggestions_For_UserA_Scan()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var authA = await RegisterAsync(clientA, "alice3@test.com", "Alice3");
        var authB = await RegisterAsync(clientB, "bob3@test.com", "Bob3");

        Authenticate(clientA, authA.Token);
        Authenticate(clientB, authB.Token);

        var scanResultId = await SeedScanForTenantAsync(authA.TenantId, authA.Email);

        var response = await clientB.PostAsJsonAsync("/api/ai/suggestions", new { scanResultId });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Stores_Files_Under_Tenant_Folder()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "uploader@test.com", "Uploader");
        Authenticate(client, auth.Token);

        using var content = new MultipartFormDataContent();
        var zipBytes = CreateMinimalZip();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", "sample.zip");

        var response = await client.PostAsync("/api/scan/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ScanUploadResponseDto>>(JsonOptions);
        Assert.NotNull(payload?.Data);
        Assert.Contains(auth.TenantId.ToString("N"), payload!.Data!.UploadedZip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NormalUser_Cannot_ListTenants_As_SuperAdmin()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterAsync(client, "normal@test.com", "Normal");
        Authenticate(client, auth.Token);

        var response = await client.GetAsync("/api/admin/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Cannot_Access_Projects()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<int> SeedScanForTenantAsync(Guid tenantId, string ownerEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<Application.Interfaces.ITenantProvider>();

        var user = db.Users.First(u => u.Email == ownerEmail.ToLowerInvariant());
        tenantProvider.SetTenant(tenantId, user.Id, "Test Org");

        var projectId = Guid.NewGuid();
        db.ScanProjects.Add(new ScanProject
        {
            Id = projectId,
            TenantId = tenantId,
            FileName = "seed.zip",
            Status = "Completed",
            UserId = user.Id,
            UploadedAt = DateTime.UtcNow
        });

        var result = new ScanResult
        {
            TenantId = tenantId,
            ScanProjectId = projectId,
            ProjectPath = "/tmp",
            Score = 80,
            Status = "Completed",
            Issues =
            [
                new ScanIssue
                {
                    TenantId = tenantId,
                    Title = "Test issue",
                    Severity = "Low",
                    RuleName = "TestRule",
                    Category = "General",
                    Description = "test",
                    RuleId = "t1",
                    FilePath = "a.cs"
                }
            ]
        };

        db.ScanResults.Add(result);
        await db.SaveChangesAsync();
        return result.Id;
    }

    private static async Task<AuthResponseDto> RegisterAsync(HttpClient client, string email, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Email = email,
            FullName = name,
            Password = "Password123!",
            OrganizationName = $"{name} Org"
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.NotEqual(Guid.Empty, auth.TenantId);
        return auth;
    }

    private static void Authenticate(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static byte[] CreateMinimalZip()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("Program.cs");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("// sample");
        }

        return ms.ToArray();
    }

    private sealed class ScanUploadResponseDto
    {
        public Guid ProjectId { get; set; }
        public string UploadedZip { get; set; } = string.Empty;
        public string ExtractedProject { get; set; } = string.Empty;
    }

    private sealed class ProjectDashboardDto
    {
        public Guid ProjectId { get; set; }
        public string FileName { get; set; } = string.Empty;
    }
}
