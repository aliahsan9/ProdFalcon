using ExamDynamicsAPI.Applications.Mappings;
using ExamDynamicsAPI.Applications.Services;
using ExamDynamicsAPI.Applications.Services.Auth;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using ExamDynamicsAPI.Infrastructure.Repositories;
using ExamDynamicsAPI.Infrastructure.Seeders;
using ExamDynamicsAPI.WebAPI.Middleware;

using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ==========================
    // Logging (Serilog)
    // ==========================
    builder.Host.UseSerilog((ctx, _, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/examdynamics-.log", rollingInterval: RollingInterval.Day));

    // ==========================
    // Database
    // ==========================
    if (builder.Environment.IsEnvironment("IntegrationTests"))
    {
        builder.Services.AddDbContext<ExamDynamicsDbContext>(options =>
            options.UseInMemoryDatabase("ExamDynamicsIntegrationTests"));
    }
    else
    {
        builder.Services.AddDbContext<ExamDynamicsDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    }

    // ==========================
    // Identity
    // ==========================
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ExamDynamicsDbContext>()
    .AddDefaultTokenProviders();

    // ==========================
    // JWT Authentication
    // ==========================
    var jwtKey = builder.Configuration["Jwt:Key"];
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? jwtIssuer;

    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
    });

    // ==========================
    // Google Login
    // ==========================
    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];

    if (!string.IsNullOrWhiteSpace(googleClientId))
    {
        authBuilder.AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleSecret!;
        });
    }

    // ==========================
    // Facebook Login
    // ==========================
    var fbAppId = builder.Configuration["Authentication:Facebook:AppId"];
    var fbSecret = builder.Configuration["Authentication:Facebook:AppSecret"];

    if (!string.IsNullOrWhiteSpace(fbAppId))
    {
        authBuilder.AddFacebook(options =>
        {
            options.AppId = fbAppId!;
            options.AppSecret = fbSecret!;
        });
    }

    // =========================
    // Services
    // =========================
    builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// AI + RAG services

builder.Services.AddSignalR();

builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IAIService, OpenAIService>();
builder.Services.AddScoped<OpenAIService>();
 
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IExternalAuthCompletionService, ExternalAuthCompletionService>();

    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    builder.Services.AddScoped<IExamService, ExamService>();
    builder.Services.AddScoped<IExamRepository, ExamRepository>();

    builder.Services.AddScoped<IQuestionService, QuestionService>();
    builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();

    builder.Services.AddScoped<IOptionService, OptionService>();
    builder.Services.AddScoped<IOptionRepository, OptionRepository>();

    builder.Services.AddScoped<IAnswerService, AnswerService>();
    builder.Services.AddScoped<IAnswerRepository, AnswerRepository>();

    builder.Services.AddScoped<IContactMessageService, ContactMessageService>();
    builder.Services.AddScoped<IContactMessageRepository, ContactMessageRepository>();

    builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
    builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
    builder.Services.AddScoped<IPerformanceService, PerformanceService>();

    builder.Services.AddScoped<IExamDynamicsUnitOfWork, ExamDynamicsUnitOfWork>();
    builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

    // ==========================
    // AutoMapper
    // ==========================
    builder.Services.AddAutoMapper(typeof(MappingProfile), typeof(AnswerProfile));

    // ==========================
    // Controllers
    // ==========================
    builder.Services.AddControllers();

    // ==========================
    // CORS
    // ==========================
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // ==========================
    // Swagger (IMPORTANT)
    // ==========================
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ExamDynamics API",
            Version = "v1"
        });

        // 🔐 JWT Support
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter: Bearer {your JWT token}"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });
    });

    var app = builder.Build();

    // ==========================
    // DB Migration + Seeding
    // ==========================
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ExamDynamicsDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, userManager, roleManager);
    }

    // ==========================
    // Middleware
    // ==========================
    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    // ✅ Swagger UI ENABLED
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ExamDynamics API v1");
        options.RoutePrefix = string.Empty; // open at root
    });

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application crashed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }