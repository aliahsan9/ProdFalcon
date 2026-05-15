namespace ExamDynamicsAPI.Core.DTOs.ExamDTOs
{
     public class ReadExamDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}