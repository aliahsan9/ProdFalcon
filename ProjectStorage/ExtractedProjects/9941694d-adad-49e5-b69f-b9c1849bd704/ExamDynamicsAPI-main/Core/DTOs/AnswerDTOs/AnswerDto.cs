namespace ExamDynamicsAPI.Core.DTOs.AnswerDTOs
{
    public class AnswerDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }

        // If Answer is related to Question
        public int QuestionId { get; set; }
    }
} 
