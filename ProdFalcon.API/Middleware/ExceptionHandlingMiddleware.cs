using System.Net;
using System.Text.Json;
using ProdFalcon.Shared.Exceptions;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
            NotImplementedException => (HttpStatusCode.NotImplemented, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new ApiErrorResponse
        {
            Success = false,
            Message = message,
            TraceId = traceId
        };

        if (_environment.IsDevelopment() && statusCode == HttpStatusCode.InternalServerError)
            payload.Errors = new Dictionary<string, string[]>
            {
                ["detail"] = [exception.Message]
            };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
