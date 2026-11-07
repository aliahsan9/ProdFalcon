using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IOpenAiSuggestionService
{
    Task<AiSuggestionsResponse> GetSuggestionsAsync(int scanResultId, CancellationToken cancellationToken = default);

    Task<AiAskResponse> AskAsync(AiAskRequest request, CancellationToken cancellationToken = default);
}

public class AiAskRequest
{
    public int ScanResultId { get; set; }
    public int? FindingId { get; set; }
    public string Question { get; set; } = string.Empty;
}

public class AiAskResponse
{
    public int ScanResultId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public bool UsedExternalModel { get; set; }
    public string Grounding { get; set; } = "Stored scan findings";
}

public class AiSuggestionsResponse
{
    public int ScanResultId { get; set; }
    public IReadOnlyList<string> FixSuggestions { get; set; } = [];
    public IReadOnlyList<string> RefactoringRecommendations { get; set; } = [];
    public IReadOnlyList<string> SecurityImprovements { get; set; } = [];
    public IReadOnlyList<string> ProductionReadinessAdvice { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}
