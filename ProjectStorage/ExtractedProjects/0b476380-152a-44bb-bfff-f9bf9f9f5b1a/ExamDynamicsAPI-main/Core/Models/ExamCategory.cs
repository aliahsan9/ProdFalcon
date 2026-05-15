using System.ComponentModel.DataAnnotations;

namespace ExamDynamicsAPI.Core.Models
{
    public class ExamCategory
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;   // e.g., "Medical", "Engineering"

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        // Relationships
        public ICollection<Exam>? Exams { get; set; }
        public int ExamId { get; set; }
    }
}
