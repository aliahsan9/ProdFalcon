namespace ExamDynamicsAPI.Core.DTOs.ExamDTOs
{
 public class ExamDto
    {
        public int ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

    }
}