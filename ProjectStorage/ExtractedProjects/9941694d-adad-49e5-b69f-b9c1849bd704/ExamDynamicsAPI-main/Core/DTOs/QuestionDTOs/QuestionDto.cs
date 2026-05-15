namespace ExamDynamicsAPI.Core.DTOs.QuestionDTOs
{
    public class QuestionDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string? Explanation { get; set; }
        public string Text { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty; 
    }
}