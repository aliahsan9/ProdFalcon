using ExamDynamicsAPI.Applications.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly OpenAIService _chatService;

    public ChatController(OpenAIService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
public async Task<IActionResult> Ask([FromBody] ChatRequest request)
{
    var answer = await _chatService.AskQuestion(request.Message);
    return Ok(answer);
}
}
}