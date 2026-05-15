using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;

namespace ProdFalcon.Infrastructure.Services;

public class OpenAiSuggestionService : IOpenAiSuggestionService
{
    private readonly IScanResultRepository _scanResultRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiSuggestionService> _logger;

    public OpenAiSuggestionService(
        IScanResultRepository scanResultRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiSuggestionService> logger)
    {
        _scanResultRepository = scanResultRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiSuggestionsResponse> GetSuggestionsAsync(int scanResultId, CancellationToken cancellationToken = default)
    {
        var scanResult = await _scanResultRepository.GetByIdAsync(scanResultId, cancellationToken)
            ?? throw new KeyNotFoundException($"Scan result {scanResultId} was not found.");

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BuildFallbackResponse(scanResultId, scanResult);

        try
        {
            var prompt = BuildPrompt(scanResult);
            var client = _httpClientFactory.CreateClient("OpenAI");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "You are a senior .NET architect. Return concise JSON only." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return BuildFallbackResponse(scanResultId, scanResult);

            return ParseAiResponse(scanResultId, text) ?? BuildFallbackResponse(scanResultId, scanResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI call failed, returning heuristic suggestions");
            return BuildFallbackResponse(scanResultId, scanResult);
        }
    }

    private static string BuildPrompt(ProdFalcon.Application.Scanning.Models.ScanResult scanResult)
    {
        var issues = scanResult.Issues.Take(40).Select(i =>
            $"- [{i.Severity}] {i.Title} ({i.RuleName}) in {i.FilePath}");

        return $"""
            Analyze this scan and return JSON with keys:
            fixSuggestions, refactoringRecommendations, securityImprovements, productionReadinessAdvice, summary

            Project path: {scanResult.ProjectPath}
            Overall score: {scanResult.Score}/100
            Security: {scanResult.SecurityScore}
            Maintainability: {scanResult.MaintainabilityScore}
            Performance: {scanResult.PerformanceScore}
            Production readiness: {scanResult.ProductionReadinessScore}

            Issues:
            {string.Join(Environment.NewLine, issues)}
            """;
    }

    private static AiSuggestionsResponse? ParseAiResponse(int scanResultId, string text)
    {
        try
        {
            var cleaned = text.Trim();
            if (cleaned.StartsWith("```"))
            {
                var start = cleaned.IndexOf('{');
                var end = cleaned.LastIndexOf('}');
                if (start >= 0 && end > start)
                    cleaned = cleaned[start..(end + 1)];
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            return new AiSuggestionsResponse
            {
                ScanResultId = scanResultId,
                FixSuggestions = ReadArray(root, "fixSuggestions"),
                RefactoringRecommendations = ReadArray(root, "refactoringRecommendations"),
                SecurityImprovements = ReadArray(root, "securityImprovements"),
                ProductionReadinessAdvice = ReadArray(root, "productionReadinessAdvice"),
                Summary = root.TryGetProperty("summary", out var summary)
                    ? summary.GetString() ?? string.Empty
                    : string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadArray(JsonElement root, string property) =>
        root.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
            : [];

    private static AiSuggestionsResponse BuildFallbackResponse(
        int scanResultId,
        ProdFalcon.Application.Scanning.Models.ScanResult scanResult)
    {
        var fixSuggestions = new List<string>();
        var security = new List<string>();
        var production = new List<string>();
        var refactor = new List<string>();

        foreach (var issue in scanResult.Issues.Take(25))
        {
            var suggestion = issue.RuleName switch
            {
                nameof(Application.Scanning.Rules.HardcodedConnectionStringRule) =>
                    "Move connection strings to appsettings.json and inject via IConfiguration.",
                nameof(Application.Scanning.Rules.SwaggerInProductionRule) =>
                    "Disable Swagger in Production environment.",
                nameof(Application.Scanning.Rules.MissingAuthorizationRule) =>
                    "Add [Authorize] attributes or authorization policies to protected endpoints.",
                nameof(Application.Scanning.Rules.ApiKeyExposureRule) =>
                    "Store API keys in user secrets, environment variables, or a vault.",
                nameof(Application.Scanning.Rules.HttpUsageRule) =>
                    "Enforce HTTPS redirection and HSTS in production.",
                _ => $"Review and remediate: {issue.Title}"
            };

            fixSuggestions.Add(suggestion);

            if (issue.Category == "Security")
                security.Add(suggestion);
            else if (issue.Category == "Production")
                production.Add(suggestion);
            else
                refactor.Add($"Refactor affected code in {issue.FilePath}");
        }

        return new AiSuggestionsResponse
        {
            ScanResultId = scanResultId,
            FixSuggestions = fixSuggestions.Distinct().Take(10).ToList(),
            RefactoringRecommendations = refactor.Distinct().Take(8).ToList(),
            SecurityImprovements = security.Distinct().Take(8).ToList(),
            ProductionReadinessAdvice = production.Concat([
                "Add centralized exception handling middleware.",
                "Enable structured logging (Serilog) with correlation IDs.",
                "Use dependency injection instead of static service access."
            ]).Distinct().Take(8).ToList(),
            Summary = $"Your project is {scanResult.Score}/100 production ready."
        };
    }
}
