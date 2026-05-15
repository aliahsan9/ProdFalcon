using ExamDynamicsAPI.Core.DTOs.PerformanceDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PerformanceController : ControllerBase
    {
        private readonly IPerformanceService _performance;

        public PerformanceController(IPerformanceService performance)
        {
            _performance = performance;
        }

        [HttpPost("exam-attempts")]
        public async Task<IActionResult> SubmitAttempt([FromBody] SubmitExamAttemptDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _performance.RecordExamAttemptAsync(User, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Exam not found." });
            }
        }

        [HttpGet("exam-attempts")]
        public async Task<IActionResult> GetAttempts([FromQuery] int take = 50)
        {
            var list = await _performance.GetRecentAttemptsAsync(User, take);
            return Ok(list);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var s = await _performance.GetSummaryAsync(User);
            return Ok(s);
        }

        [HttpGet("chart")]
        public async Task<IActionResult> GetChart([FromQuery] int days = 30)
        {
            var data = await _performance.GetChartSeriesAsync(User, days);
            return Ok(data);
        }

        [HttpGet("activity")]
        public async Task<IActionResult> GetActivity([FromQuery] int take = 30)
        {
            var data = await _performance.GetRecentActivityAsync(User, take);
            return Ok(data);
        }

        [HttpGet("certificate/{attemptId:int}")]
        public async Task<IActionResult> GetCertificate(int attemptId)
        {
            var cert = await _performance.GetCertificateAsync(User, attemptId);
            return cert == null ? NotFound() : Ok(cert);
        }
    }
}
