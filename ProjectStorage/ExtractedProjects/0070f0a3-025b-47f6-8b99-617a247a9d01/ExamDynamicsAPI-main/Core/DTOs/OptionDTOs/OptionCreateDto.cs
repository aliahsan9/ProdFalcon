namespace ExamDynamicsAPI.Core.DTOs.OptionDTOs
{
    public class OptionCreateDto
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int QuestionId { get; set; }
    }
}