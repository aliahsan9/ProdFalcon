using AutoMapper;
using ExamDynamicsAPI.Core.DTOs.OptionDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = "Admin")]
    public class OptionController : ControllerBase
    {
        private readonly IOptionService _service;
        private readonly IMapper _mapper;

        public OptionController(IOptionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }
    //    [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OptionDto>>> GetAll()
        {
            var options = await _service.GetAllAsync();
            return Ok(options);
        }
        // [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<OptionDto>> GetById(int id)
        {
            var option = await _service.GetByIdAsync(id);
            if (option == null) return NotFound();

            return Ok(option);
        }

        [HttpPost]
        public async Task<ActionResult<OptionDto>> Create([FromBody] OptionCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var option = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = option.OptionId }, option);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] OptionUpdateDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var updated = await _service.UpdateAsync(id, dto);
            if (!updated) return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();

            return NoContent();
        }

        [HttpGet("question/{questionId}")]
        public async Task<ActionResult<IEnumerable<OptionDto>>> GetByQuestionId(int questionId)
        {
            var options = await _service.GetByQuestionIdAsync(questionId);
            return Ok(options);
        }
    }
}
