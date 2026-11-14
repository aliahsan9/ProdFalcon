using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Remediation;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[EnableRateLimiting("ai")]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IOpenAiSuggestionService _suggestionService;
    private readonly IRemediationService _remediation;

    public AiController(IOpenAiSuggestionService suggestionService, IRemediationService remediation)
    {
        _suggestionService = suggestionService;
        _remediation = remediation;
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

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AiAskRequest request, CancellationToken cancellationToken)
    {
        if (request.ScanResultId <= 0 || string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<AiAskResponse>.Fail("A scan and a question are required."));

        try
        {
            var answer = await _suggestionService.AskAsync(request, cancellationToken);
            return Ok(ApiResponse<AiAskResponse>.Ok(answer));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<AiAskResponse>.Fail("Scan result not found."));
        }
    }

    [HttpGet("remediation/{scanResultId:int}")]
    public async Task<IActionResult> GetRemediation(int scanResultId, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _remediation.GetAsync(scanResultId, cancellationToken);
            return Ok(ApiResponse<RemediationPlanDto>.Ok(plan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<RemediationPlanDto>.Fail("Scan result not found."));
        }
    }

    [HttpPost("remediation")]
    public async Task<IActionResult> BuildRemediation([FromBody] AiSuggestionRequest request, CancellationToken cancellationToken)
    {
        if (request.ScanResultId <= 0)
            return BadRequest(ApiResponse<RemediationPlanDto>.Fail("ScanResultId is required."));

        try
        {
            var plan = await _remediation.BuildAsync(request.ScanResultId, cancellationToken);
            return Ok(ApiResponse<RemediationPlanDto>.Ok(plan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<RemediationPlanDto>.Fail("Scan result not found."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RemediationPlanDto>.Fail(ex.Message));
        }
    }

    [HttpPost("remediation/{proposalId:int}/decision")]
    public async Task<IActionResult> DecideRemediation(int proposalId, [FromBody] RemediationDecisionRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Decision))
            return BadRequest(ApiResponse<RemediationProposalDto>.Fail("Decision must be Approved or Rejected."));

        try
        {
            var proposal = await _remediation.DecideAsync(proposalId, request.Decision.Trim(), request.Note, cancellationToken);
            return Ok(ApiResponse<RemediationProposalDto>.Ok(proposal, "Decision recorded. The repository was not modified."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<RemediationProposalDto>.Fail("Remediation proposal was not found."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<RemediationProposalDto>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RemediationProposalDto>.Fail(ex.Message));
        }
    }
}

public class RemediationDecisionRequest
{
    public string Decision { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public class AiSuggestionRequest
{
    public int ScanResultId { get; set; }
}
