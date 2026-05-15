using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProdFalcon.Infrastructure.DependencyInjection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// ========================================
// Services
// ========================================

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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
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