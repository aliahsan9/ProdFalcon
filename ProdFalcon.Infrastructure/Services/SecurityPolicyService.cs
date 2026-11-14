using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Security;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class SecurityPolicyService : ISecurityPolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;

    public SecurityPolicyService(ApplicationDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SecurityPolicyDto> GetAsync(CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        var policy = await _db.SecurityPolicies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return policy == null ? FromSettings(QualityGateSettings.Default, DateTime.UtcNow) : ToDto(policy);
    }

    public async Task<SecurityPolicyDto> UpdateAsync(SecurityPolicyDto request, CancellationToken cancellationToken = default)
    {
        RequireSignedIn();
        TenantAccess.Require(
            TenantAccess.CanAdminister(_tenant.Role, _tenant.IsSuperAdmin),
            "Only admins and owners can change the quality gate.");

        var policy = await _db.SecurityPolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy == null)
        {
            policy = new SecurityPolicy { TenantId = _tenant.TenantId };
            _db.SecurityPolicies.Add(policy);
        }

        policy.Enabled = request.Enabled;
        policy.FailOnCritical = request.FailOnCritical;
        policy.FailOnHigh = request.FailOnHigh;
        policy.MaxOpenCritical = Clamp(request.MaxOpenCritical, 0, 10000);
        policy.MaxOpenHigh = Clamp(request.MaxOpenHigh, 0, 10000);
        policy.MinOverallScore = Clamp(request.MinOverallScore, 0, 100);
        policy.MinSecurityScore = Clamp(request.MinSecurityScore, 0, 100);
        policy.PreventScoreRegression = request.PreventScoreRegression;
        policy.FailOnNewSecrets = request.FailOnNewSecrets;
        policy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(policy);
    }

    private void RequireSignedIn()
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Not authenticated.");
    }

    private static SecurityPolicyDto ToDto(SecurityPolicy policy)
    {
        var dto = FromSettings(new QualityGateSettings(
            policy.Enabled,
            policy.FailOnCritical,
            policy.FailOnHigh,
            policy.MaxOpenCritical,
            policy.MaxOpenHigh,
            policy.MinOverallScore,
            policy.MinSecurityScore,
            policy.PreventScoreRegression,
            policy.FailOnNewSecrets), policy.UpdatedAt);
        return dto;
    }

    private static SecurityPolicyDto FromSettings(QualityGateSettings settings, DateTime updatedAt) => new()
    {
        Enabled = settings.Enabled,
        FailOnCritical = settings.FailOnCritical,
        FailOnHigh = settings.FailOnHigh,
        MaxOpenCritical = settings.MaxOpenCritical,
        MaxOpenHigh = settings.MaxOpenHigh,
        MinOverallScore = settings.MinOverallScore,
        MinSecurityScore = settings.MinSecurityScore,
        PreventScoreRegression = settings.PreventScoreRegression,
        FailOnNewSecrets = settings.FailOnNewSecrets,
        UpdatedAt = updatedAt,
        Rules = QualityGateEvaluator.Describe(settings)
    };

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}
