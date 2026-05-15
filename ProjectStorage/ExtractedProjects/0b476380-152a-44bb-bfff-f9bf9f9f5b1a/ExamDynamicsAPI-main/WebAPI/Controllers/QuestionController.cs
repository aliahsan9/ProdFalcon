using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = "Admin")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _questionService;

        public QuestionController(IQuestionService questionService)
        {
            _questionService = questionService;
        }
        //  [AllowAnonymous]
        // GET: api/Question
        [HttpGet]
        public async Task<IActionResult> GetAllQuestions()
        {
            var questions = await _questionService.GetAllAsync();
            return Ok(questions);
        }
    //    [AllowAnonymous]
        // GET: api/Question/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuestionById(int id)
        {
            var question = await _questionService.GetByIdAsync(id);
            if (question == null)
                return NotFound();

            return Ok(question);
        }

        // POST: api/Question
        [HttpPost]
        public async Task<IActionResult> AddQuestion([FromBody] Question question)
        {
            var createdQuestion = await _questionService.CreateAsync(question); // Use CreateAsync
            return Ok(createdQuestion);
        }

        // PUT: api/Question
        [HttpPut]
        public async Task<IActionResult> UpdateQuestion([FromBody] Question question)
        {
            var updatedQuestion = await _questionService.UpdateAsync(question);
            if (updatedQuestion == null)
                return NotFound();
            return Ok(updatedQuestion);
        }

        // DELETE: api/Question/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var result = await _questionService.DeleteAsync(id);
            if (!result)
                return NotFound();

            return Ok(new { message = "Question deleted successfully" });
        }
    }
}
