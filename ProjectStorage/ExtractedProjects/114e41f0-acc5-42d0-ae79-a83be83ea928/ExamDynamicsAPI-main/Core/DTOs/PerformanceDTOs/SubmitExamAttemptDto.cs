using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.DTOs.PerformanceDTOs
{
    public class SubmitExamAttemptDto
    {
        [Required]
        public int ExamId { get; set; }

        [Required, MaxLength(200)]
        public string ExamTitle { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Score { get; set; }

        [Range(1, int.MaxValue)]
        public int TotalQuestions { get; set; }
    }
}
