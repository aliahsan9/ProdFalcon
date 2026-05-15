using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Rules;
using ProdFalcon.Application.Scanning.Services;
using ProdFalcon.Infrastructure.DependencyInjection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// ========================================
// Services
// ========================================

// ===============================
// CORE SERVICE
// ===============================
builder.Services.AddScoped<IProjectScanner, ProjectScanner>();

// ===============================
// SCAN RULES REGISTRATION
// ===============================
builder.Services.AddScoped<IScanRule, HardcodedConnectionStringRule>();
builder.Services.AddScoped<IScanRule, HardcodedJwtSecretRule>();
builder.Services.AddScoped<IScanRule, ApiKeyExposureRule>();
builder.Services.AddScoped<IScanRule, DebugModeRule>();
builder.Services.AddScoped<IScanRule, SqlInjectionRiskRule>();
builder.Services.AddScoped<IScanRule, HttpUsageRule>();
builder.Services.AddScoped<IScanRule, SensitiveLoggingRule>();
builder.Services.AddScoped<IScanRule, CorsWildcardRule>();
builder.Services.AddScoped<IScanRule, MissingAuthorizationRule>();
builder.Services.AddScoped<IScanRule, PlainTextPasswordRule>();

// Controllers
builder.Services.AddControllers();

// Infrastructure (DbContext, Repos, etc.)
builder.Services.AddInfrastructure(builder.Configuration);


// ========================================
// JWT Authentication
// ========================================

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty))
    };
});

builder.Services.AddAuthorization();


// ========================================
// Swagger
// ========================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ========================================
// Build App
// ========================================

var app = builder.Build();


// ========================================
// Middleware Pipeline
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();   // MUST come before Authorization
app.UseAuthorization();

app.MapControllers();

app.Run();