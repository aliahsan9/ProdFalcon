using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ProdFalcon.API.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddProdFalconRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();
        services.AddSingleton(settings);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => WriteRejectionAsync(context, cancellationToken);

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (IsExempt(http))
                    return RateLimitPartition.GetNoLimiter("exempt");

                return FixedWindow($"global:{PartitionKey(http)}", settings.GlobalPermitLimit, settings.WindowSeconds);
            });

            options.AddPolicy("auth", http =>
                FixedWindow($"auth:{PartitionKey(http)}", settings.AuthPermitLimit, settings.WindowSeconds));

            options.AddPolicy("scans", http =>
                FixedWindow($"scan:{PartitionKey(http)}", settings.ScanPermitLimit, settings.WindowSeconds));

            options.AddPolicy("ai", http =>
                FixedWindow($"ai:{PartitionKey(http)}", settings.AiPermitLimit, settings.WindowSeconds));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });

    private static string PartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            return "user:" + userId;

        return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private static bool IsExempt(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;
        return path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
               && path.EndsWith("/version", StringComparison.OrdinalIgnoreCase);
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        if (http.Response.HasStarted)
            return;

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ProdFalcon.RateLimiting");
        logger.LogWarning(
            "Rate limit exceeded for {Method} {Path} from {Partition}",
            http.Request.Method,
            http.Request.Path.Value,
            PartitionKey(http));

        await http.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Too many requests. Wait a moment and try again."
        }, cancellationToken);
    }
}
