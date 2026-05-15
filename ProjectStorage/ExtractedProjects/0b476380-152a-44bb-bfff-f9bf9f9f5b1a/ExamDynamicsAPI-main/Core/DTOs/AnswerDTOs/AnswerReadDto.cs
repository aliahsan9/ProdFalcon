namespace ExamDynamicsAPI.Core.DTOs.AnswerDTOs
{
    public class AnswerReadDto
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}