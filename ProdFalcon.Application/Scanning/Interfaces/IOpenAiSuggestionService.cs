using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IOpenAiSuggestionService
{
    Task<AiSuggestionsResponse> GetSuggestionsAsync(int scanResultId, CancellationToken cancellationToken = default);
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
