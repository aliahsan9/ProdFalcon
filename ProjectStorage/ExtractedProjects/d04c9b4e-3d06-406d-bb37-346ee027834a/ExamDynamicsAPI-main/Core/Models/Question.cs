using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Core.Models
{
    public class Question
    {
        [Key]
        public int QuestionId { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        [Required]
        public string CorrectAnswer { get; set; } = string.Empty;
        public string? Explanation { get; set; }

        // Optional: Foreign key to Exam
        public int? ExamId { get; set; }

        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }

        // Relations
        public ICollection<Option>? Options { get; set; }

          // Add this for Answers
        public ICollection<Answer>? Answers { get; set; }
    
  }
}