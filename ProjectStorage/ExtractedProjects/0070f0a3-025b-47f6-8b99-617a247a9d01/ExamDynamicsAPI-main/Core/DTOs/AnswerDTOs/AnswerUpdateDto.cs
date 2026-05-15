using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.DTOs.AnswerDTOs
{
    public class AnswerUpdateDto
    {
        [Required]
        public string Text { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }
}