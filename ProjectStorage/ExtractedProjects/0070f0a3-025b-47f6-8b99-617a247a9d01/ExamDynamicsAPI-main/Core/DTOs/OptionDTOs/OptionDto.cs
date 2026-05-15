namespace ExamDynamicsAPI.Core.DTOs.OptionDTOs
{
    public class OptionDto
    {
        public int OptionId { get; set; }
        public string Text { get; set; } = null!;
        public bool IsCorrect { get; set; }
        public int QuestionId { get; set; }
    }
}