using ExamDynamicsAPI.Core.DTOs.ProfileDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamDynamicsAPI.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentProfileController : ControllerBase
    {
        private readonly IStudentProfileService _profileService;

        public StudentProfileController(IStudentProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var profile = await _profileService.GetProfileAsync(User);
            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateStudentProfileDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var profile = await _profileService.UpdateProfileAsync(User, dto);
                return profile == null ? NotFound() : Ok(profile);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
