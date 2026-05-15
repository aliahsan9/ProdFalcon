namespace ExamDynamicsAPI.Core.DTOs.QuestionDTOs
{
    public class QuestionReadDTO
    {
        public int Id { get; set; }
        public string? Explanation { get; set; }
        public string Text { get; set; } = string.Empty;
        public int SubjectId { get; set; }
    }
}