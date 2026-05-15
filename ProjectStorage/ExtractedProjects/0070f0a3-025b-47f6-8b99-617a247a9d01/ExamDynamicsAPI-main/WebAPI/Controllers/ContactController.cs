using ExamDynamicsAPI.Core.DTOs.ContactMessageDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IContactMessageService _service;

        public ContactController(IContactMessageService service)
        {
            _service = service;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ContactMessageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(dto.ResolvedEmail))
                return BadRequest(new { message = "Email is required." });

            await _service.SendMessageAsync(dto);
            return Ok(new { message = "Your message has been sent successfully!" });
        }
    }
}
