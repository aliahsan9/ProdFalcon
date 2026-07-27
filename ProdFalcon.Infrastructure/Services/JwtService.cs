using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Infrastructure.Tenancy;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProdFalcon.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(JwtTenantContext context)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, context.User.Id.ToString()),
            new(ClaimTypes.NameIdentifier, context.User.Id.ToString()),
            new(ClaimTypes.Email, context.User.Email),
            new(ClaimTypes.Name, context.User.FullName),
            new(TenantClaimTypes.TenantId, context.Tenant.Id.ToString()),
            new(TenantClaimTypes.Organization, context.Tenant.Name),
            new(TenantClaimTypes.Role, context.Role.ToString()),
            new(ClaimTypes.Role, context.Role.ToString()),
            new(TenantClaimTypes.Plan, context.Plan.ToString()),
            new(TenantClaimTypes.IsSuperAdmin, context.User.IsSuperAdmin ? "true" : "false")
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.")));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
