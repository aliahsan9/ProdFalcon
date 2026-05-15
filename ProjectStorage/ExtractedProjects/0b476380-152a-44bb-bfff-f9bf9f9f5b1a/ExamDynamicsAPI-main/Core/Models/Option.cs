using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamDynamicsAPI.Core.Models
{
    public class Option
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OptionId { get; set; }

        [Required]
        public string Text { get; set; } = null!;

        public bool IsCorrect { get; set; }

        // Foreign Key to Question
        [Required]
        public int QuestionId { get; set; }
        public Question? Question { get; set; }

        // Relation: Option - Answers (1:Many)
        public ICollection<Answer>? Answers { get; set; }
    }
}
