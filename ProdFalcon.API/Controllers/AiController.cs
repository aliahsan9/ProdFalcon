using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IOpenAiSuggestionService _suggestionService;

    public AiController(IOpenAiSuggestionService suggestionService)
    {
        _suggestionService = suggestionService;
    }

    [HttpPost("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromBody] AiSuggestionRequest request, CancellationToken cancellationToken)
    {
        if (request.ScanResultId <= 0)
            return BadRequest(ApiResponse<AiSuggestionsResponse>.Fail("ScanResultId is required."));

        try
        {
            var suggestions = await _suggestionService.GetSuggestionsAsync(request.ScanResultId, cancellationToken);
            return Ok(ApiResponse<AiSuggestionsResponse>.Ok(suggestions));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<AiSuggestionsResponse>.Fail("Scan result not found."));
        }
    }
}

public class AiSuggestionRequest
{
    public int ScanResultId { get; set; }
}
