using Microsoft.Extensions.DependencyInjection;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;

namespace ProdFalcon.Application.DependencyInjection;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IScanRuleExecutor, ScanRuleExecutor>();
        services.AddScoped<IRiskScoringService, RiskScoringService>();
        services.AddScoped<IProjectScanner, ProjectScanner>();

        RegisterScanRules(services);

        return services;
    }

    private static void RegisterScanRules(IServiceCollection services)
    {
        services.AddScoped<IScanRule, HardcodedConnectionStringRule>();
        services.AddScoped<IScanRule, HardcodedJwtSecretRule>();
        services.AddScoped<IScanRule, ApiKeyExposureRule>();
        services.AddScoped<IScanRule, DebugModeRule>();
        services.AddScoped<IScanRule, SqlInjectionRiskRule>();
        services.AddScoped<IScanRule, HttpUsageRule>();
        services.AddScoped<IScanRule, SensitiveLoggingRule>();
        services.AddScoped<IScanRule, CorsWildcardRule>();
        services.AddScoped<IScanRule, MissingAuthorizationRule>();
        services.AddScoped<IScanRule, PlainTextPasswordRule>();
        services.AddScoped<IScanRule, SwaggerInProductionRule>();
        services.AddScoped<IScanRule, MissingLoggingRule>();
    }
}
