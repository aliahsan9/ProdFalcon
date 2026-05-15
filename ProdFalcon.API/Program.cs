using Microsoft.EntityFrameworkCore;
using ProdFalcon.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);


// ========================================
// Add Services to Container
// ========================================

// Controllers
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();


// ========================================
// Dependency Injection Registrations
// ========================================

// Example:
// builder.Services.AddScoped<IAuthService, AuthService>();
// builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// builder.Services.AddScoped<IJwtService, JwtService>();


// ========================================
// Build App
// ========================================

var app = builder.Build();


// ========================================
// Configure HTTP Request Pipeline
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}


// HTTPS
app.UseHttpsRedirection();


// Authentication & Authorization
// app.UseAuthentication();
 
app.UseAuthorization();


// Map Controllers
app.MapControllers();


// Run Application
app.Run();