using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ProdFalcon.API.Middleware;
using ProdFalcon.Application.DependencyInjection;
using ProdFalcon.Infrastructure.DependencyInjection;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Prevent a background-service cancel during shutdown from stopping the whole host.
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SubscriptionValidationMiddleware>();
app.MapControllers();

var urls = app.Urls.Any()
    ? string.Join(", ", app.Urls)
    : builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5014";

Log.Information("ProdFalcon API listening on {Urls}", urls);
Log.Information("Swagger UI: http://localhost:5014/swagger");

try
{
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                             || ex.InnerException?.Message?.Contains("address already in use", StringComparison.OrdinalIgnoreCase) == true)
{
    Log.Fatal(
        "Cannot start: port 5014 is already in use. " +
        "Stop the other API instance (Task Manager → ProdFalcon.API) or run: .\\scripts\\Stop-ProdFalconApi.ps1");
    throw;
}

public partial class Program { }

