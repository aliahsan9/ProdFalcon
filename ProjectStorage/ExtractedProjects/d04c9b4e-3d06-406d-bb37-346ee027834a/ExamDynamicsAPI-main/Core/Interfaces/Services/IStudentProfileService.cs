using ExamDynamicsAPI.Core.DTOs.ProfileDTOs;
using System.Security.Claims;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IStudentProfileService
    {
        Task<StudentProfileDto?> GetProfileAsync(ClaimsPrincipal user);
        Task<StudentProfileDto?> UpdateProfileAsync(ClaimsPrincipal user, UpdateStudentProfileDto dto);
    }
}
