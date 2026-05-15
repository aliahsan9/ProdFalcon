using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.Models
{
    public class Exam
    {
        [Key]
        public int ExamId { get; set; } 

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ExamAttempt> Attempts { get; set; } = new HashSet<ExamAttempt>();
    }
}
