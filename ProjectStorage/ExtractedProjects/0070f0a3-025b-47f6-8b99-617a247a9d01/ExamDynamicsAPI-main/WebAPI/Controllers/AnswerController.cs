using ExamDynamicsAPI.Core.DTOs.AnswerDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = "Admin")] // Only admins can create/update/delete
    public class AnswerController : ControllerBase
    {
        private readonly IAnswerService _answerService;

        public AnswerController(IAnswerService answerService)
        {
            _answerService = answerService;
        }

        // ================= CREATE =================
        [HttpPost]
        public async Task<ActionResult<AnswerReadDto>> Create([FromBody] AnswerCreateDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized("User not found in token.");

            if (!int.TryParse(userIdStr, out int userId))
                return BadRequest("Invalid user ID.");

            var answer = await _answerService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = answer.Id }, answer);
        }

        // [AllowAnonymous]
        // ================= GET BY ID =================
        [HttpGet("{id}")]
        public async Task<ActionResult<AnswerReadDto>> GetById(int id)
        {
            var answer = await _answerService.GetByIdAsync(id);
            if (answer == null) return NotFound();
            return Ok(answer);
        }

        // [AllowAnonymous]
        // ================= GET ALL =================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnswerReadDto>>> GetAll()
        {
            var answers = await _answerService.GetAllAsync();
            return Ok(answers);
        }

        // ================= UPDATE =================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AnswerUpdateDto dto)
        {
            var success = await _answerService.UpdateAsync(id, dto);
            if (!success) return NotFound();
            return NoContent();
        }

        // ================= DELETE =================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _answerService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
