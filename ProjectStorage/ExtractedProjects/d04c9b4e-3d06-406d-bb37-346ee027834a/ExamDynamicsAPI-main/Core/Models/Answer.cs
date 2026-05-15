using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamDynamicsAPI.Core.Models
{
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to ApplicationUser
        [Required]
        public int UserId { get; set; }  

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        // Foreign Key to Question
        [Required]
        public int QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public Question Question { get; set; } = null!;

        public int? OptionId { get; set; }

        [ForeignKey(nameof(OptionId))]
        public Option? Option { get; set; }

        [MaxLength(1000)]
        public string? Text { get; set; }

        public bool IsCorrect { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
  