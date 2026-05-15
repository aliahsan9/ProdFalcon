using AutoMapper;
using ExamDynamicsAPI.Core.DTOs.ExamDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController] 
    // [Authorize(Roles = "Admin")]
    public class ExamController : ControllerBase
    {
        private readonly IExamService _examService;
        private readonly IMapper _mapper;

        public ExamController(IExamService examService, IMapper mapper)
        {
            _examService = examService;
            _mapper = mapper;
        }
        // [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExamDto>>> GetAll()
        {
            var exams = await _examService.GetAllAsync();
            return Ok(exams);
        }
        //  [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<ExamDto>> GetById(int id)
        {
            var exam = await _examService.GetByIdAsync(id);
            if (exam == null) return NotFound();
            return Ok(exam);
        }

        [HttpPost]
        public async Task<ActionResult<ExamDto>> Create([FromBody] CreateExamDto dto)
        {
            var exam = await _examService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = exam.ExamId }, exam);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateExamDto dto)
        {
            var success = await _examService.UpdateAsync(id, dto);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _examService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
