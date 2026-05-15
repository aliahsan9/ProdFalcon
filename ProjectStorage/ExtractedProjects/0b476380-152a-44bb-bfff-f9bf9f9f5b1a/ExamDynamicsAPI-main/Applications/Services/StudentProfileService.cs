using ExamDynamicsAPI.Core.DTOs.ProfileDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ExamDynamicsAPI.Applications.Services
{
    public class StudentProfileService : IStudentProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _activityLog;

        public StudentProfileService(UserManager<ApplicationUser> userManager, IActivityLogService activityLog)
        {
            _userManager = userManager;
            _activityLog = activityLog;
        }

        public async Task<StudentProfileDto?> GetProfileAsync(ClaimsPrincipal principal)
        {
            var user = await _userManager.GetUserAsync(principal);
            return user == null ? null : Map(user);
        }

        public async Task<StudentProfileDto?> UpdateProfileAsync(ClaimsPrincipal principal, UpdateStudentProfileDto dto)
        {
            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return null;

            user.FullName = dto.FullName.Trim();
            user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
            user.Institution = string.IsNullOrWhiteSpace(dto.Institution) ? null : dto.Institution.Trim();
            user.Country = string.IsNullOrWhiteSpace(dto.Country) ? null : dto.Country.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _activityLog.LogAsync(user.Id, "ProfileUpdated", "Profile details were updated.");

            return Map(user);
        }

        private static StudentProfileDto Map(ApplicationUser u) => new()
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email ?? string.Empty,
            FullName = u.FullName,
            Bio = u.Bio,
            Institution = u.Institution,
            Country = u.Country,
            CreatedAt = u.CreatedAt
        };
    }
}
