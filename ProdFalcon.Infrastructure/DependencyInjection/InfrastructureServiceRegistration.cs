using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
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
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddHttpClient("OpenAI", client =>
        {
            client.BaseAddress = new Uri(configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(60);
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
        services.AddHostedService<StorageCleanupHostedService>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}
