namespace ExamDynamicsAPI.Core.DTOs.QuestionDTOs
{
     public class CreateQuestionDto
    {
        public int ExamId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public string QuestionType { get; set; } = string.Empty;
    }

}