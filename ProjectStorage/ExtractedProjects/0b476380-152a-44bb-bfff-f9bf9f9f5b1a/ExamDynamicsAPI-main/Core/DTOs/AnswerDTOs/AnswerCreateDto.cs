using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.DTOs.AnswerDTOs
{
    public class AnswerCreateDto
    {
        [Required]
        public int QuestionId { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        public bool IsCorrect { get; set; } = false;
    }
} 
