using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.DTOs.ProfileDTOs
{
    public class UpdateStudentProfileDto
    {
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(200)]
        public string? Institution { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }
    }
}
