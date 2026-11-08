using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Acquisition;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;
using ProdFalcon.Infrastructure.Background;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Infrastructure.Repositories;
using ProdFalcon.Infrastructure.Services;
using ProdFalcon.Infrastructure.Tenancy;

namespace ProdFalcon.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql =>
                {
                    sql.CommandTimeout(60);
                    // Parent rows such as ScanResult.AnalysisDocument must not be repeated once per issue.
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                }));

        services.Configure<ScanLimitsOptions>(configuration.GetSection(ScanLimitsOptions.SectionName));
        services.AddScoped(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScanLimitsOptions>>().Value);

        services.AddHttpClient("OpenAI", client =>
        {
            client.BaseAddress = new Uri(configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("GitHub", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddHttpClient("OSV", client =>
        {
            client.BaseAddress = new Uri("https://api.osv.dev/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ProdFalcon");
        });

        services.AddScoped<ITenantProvider, HttpTenantProvider>();
        services.AddScoped<ITenantCacheKeyBuilder, TenantCacheKeyBuilder>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantMemberService, TenantMemberService>();
        services.AddScoped<ISuperAdminService, SuperAdminService>();
        services.AddScoped<IProjectStorageService, ProjectStorageService>();
        services.AddScoped<IScanProjectRepository, ScanProjectRepository>();
        services.AddScoped<IScanResultRepository, ScanResultRepository>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOpenAiSuggestionService, OpenAiSuggestionService>();
        services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
        services.AddScoped<IGitHubIntegrationService, GitHubIntegrationService>();
        services.AddScoped<IRepositoryAcquisitionService, GitHubRepositoryClient>();
        services.AddScoped<IScanQueryService, ScanQueryService>();
        services.AddScoped<IProjectWorkspaceStore, ProjectWorkspaceStore>();
        services.AddScoped<IScanRule, DependencyVulnerabilityRule>();
        services.AddSingleton<ScanQueue>();
        services.AddSingleton<IScanQueue>(sp => sp.GetRequiredService<ScanQueue>());
        services.AddHostedService<StorageCleanupHostedService>();
        services.AddHostedService<ScanWorkerHostedService>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}
