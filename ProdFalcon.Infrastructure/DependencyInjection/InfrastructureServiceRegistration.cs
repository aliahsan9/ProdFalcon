using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;
using ProdFalcon.Application.Services;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Infrastructure.Repositories;
using ProdFalcon.Infrastructure.Services;

namespace ProdFalcon.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectScanner, ProjectScanner>();
        services.AddScoped<IScanSessionRepository, ScanSessionRepository>();
        services.AddScoped<IScanRule, SwaggerInProductionRule>();
        services.AddScoped<IScanRule, MissingLoggingRule>();
        services.AddScoped<IProjectScanner, ProjectScanner>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IScanIssueRepository, ScanIssueRepository>();
        services.AddScoped<IScanResultRepository, ScanResultRepository>();

        return services;
    }
}