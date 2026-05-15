namespace ExamDynamicsAPI.Core.DTOs.QuestionDTOs
{
     public class UpdateQuestionDto
    {
        public string Text { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public string QuestionType { get; set; } = string.Empty;
    }
}