namespace ExamDynamicsAPI.Core.DTOs.ExamDTOs
{
     public class CreateExamDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ExamCategoryId { get; set; }
    }

}