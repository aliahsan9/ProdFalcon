using System.Text.Json;
using ProdFalcon.Application.Scanning.Performance;
using ProdFalcon.Application.Scanning.Quality;
using ProdFalcon.Application.Scanning.Threats;

namespace ProdFalcon.Application.Scanning.Intelligence;

public static class ProjectAnalysisPipeline
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ProjectAnalysis Analyze(string projectPath, IReadOnlyList<Models.ScanIssue>? knownIssues = null)
    {
        var profile = ProjectIntelligenceEngine.Analyze(projectPath);
        var architecture = ArchitectureGraphBuilder.Build(projectPath, profile);
        var additional = new List<Models.ScanIssue>();
        if (knownIssues == null)
        {
            additional.AddRange(CodeQualityAnalyzer.Analyze(projectPath));
            additional.AddRange(PerformanceReliabilityAnalyzer.Analyze(projectPath));
        }

        var threats = ThreatModelEngine.Build(profile, architecture, knownIssues ?? additional);
        return new ProjectAnalysis
        {
            Profile = profile,
            Architecture = architecture,
            Threats = threats,
            AdditionalIssues = additional,
            UnverifiedAreas =
            [
                "No runtime exploit was executed.",
                "No benchmark or profiler was run.",
                "Test coverage was not measured.",
                "Dependency advisories outside the configured scanners were not queried by this profile."
            ]
        };
    }

    public static ProjectAnalysisDocument? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ProjectAnalysisDocument>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
