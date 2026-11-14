using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.GitHub;
using ProdFalcon.Shared.Responses;

namespace ProdFalcon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/collaboration")]
public class CollaborationController : ControllerBase
{
    private readonly ICollaborationService _collaboration;

    public CollaborationController(ICollaborationService collaboration)
    {
        _collaboration = collaboration;
    }

    [HttpGet("notes")]
    public async Task<IActionResult> List([FromQuery] int? scanResultId, CancellationToken cancellationToken)
    {
        var notes = await _collaboration.ListAsync(scanResultId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CollaborationNoteDto>>.Ok(notes));
    }

    [HttpPost("notes")]
    public async Task<IActionResult> Add([FromBody] CollaborationNoteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var note = await _collaboration.AddAsync(request.ScanResultId, request.ProjectId, request.Body ?? string.Empty, cancellationToken);
            return Ok(ApiResponse<CollaborationNoteDto>.Ok(note, "Note saved."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<CollaborationNoteDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CollaborationNoteDto>.Fail(ex.Message));
        }
    }
}

public class CollaborationNoteRequest
{
    public int? ScanResultId { get; set; }

    public Guid? ProjectId { get; set; }

    public string? Body { get; set; }
}
