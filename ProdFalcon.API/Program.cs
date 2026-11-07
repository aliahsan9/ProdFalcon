using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProdFalcon.API.Middleware;
using ProdFalcon.API.RateLimiting;
using ProdFalcon.API.Versioning;
using ProdFalcon.Application.DependencyInjection;
using ProdFalcon.Infrastructure.Data;
using ProdFalcon.Infrastructure.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ProdFalcon.API"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new VersionedRouteConvention());
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
})
.AddMvc(options => options.Conventions.Add(new DefaultApiVersionConvention()))
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProdFalcon API",
        Version = "v1",
        Description = "Send X-Api-Version: 1.0, ?api-version=1.0, or call /api/v1/... Existing /api/... routes stay on v1."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT from POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.CustomOperationIds(description =>
    {
        var action = description.ActionDescriptor as ControllerActionDescriptor;
        var path = description.RelativePath ?? "endpoint";
        var versioned = path.Contains("{version}", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/v1", StringComparison.OrdinalIgnoreCase);
        var name = action == null ? "endpoint" : $"{action.ControllerName}_{action.ActionName}";
        return $"{name}_{description.HttpMethod}_{(versioned ? "versioned" : "default")}";
    });
});

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
builder.Services.AddHealthChecks();
builder.Services.AddProdFalconRateLimiting(builder.Configuration);

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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms CorrelationId={CorrelationId} UserId={UserId}";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        diagnosticContext.Set(
            "UserId",
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
    };
});
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProdFalcon API v1");
    });
}

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
    app.UseRateLimiter();

app.UseMiddleware<SubscriptionValidationMiddleware>();
app.MapControllers();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    version = ApiVersionInfo.ProductVersion,
    apiVersion = ApiVersionInfo.Current
}))
.AllowAnonymous()
.DisableRateLimiting();

app.MapGet("/health", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var connected = await db.Database.CanConnectAsync(cancellationToken);
    var body = new
    {
        status = connected ? "Healthy" : "Unhealthy",
        version = ApiVersionInfo.ProductVersion,
        apiVersion = ApiVersionInfo.Current
    };

    return connected
        ? Results.Ok(body)
        : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous()
.DisableRateLimiting();

var urls = app.Urls.Any()
    ? string.Join(", ", app.Urls)
    : builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5014";

Log.Information(
    "ProdFalcon API {Version} (API v{ApiVersion}) listening on {Urls}",
    ApiVersionInfo.ProductVersion,
    ApiVersionInfo.Current,
    urls);
Log.Information("Swagger UI: http://localhost:5014/swagger");

try
{
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                             || ex.InnerException?.Message?.Contains("address already in use", StringComparison.OrdinalIgnoreCase) == true)
{
    Log.Fatal(
        ex,
        "Cannot start: port 5014 is already in use. Stop the other ProdFalcon.API process and run again.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
