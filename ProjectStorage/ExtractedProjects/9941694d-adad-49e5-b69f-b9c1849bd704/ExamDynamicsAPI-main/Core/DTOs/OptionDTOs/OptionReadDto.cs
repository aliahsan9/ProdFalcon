namespace ExamDynamicsAPI.Core.DTOs.OptionDTOs
{
    public class OptionReadDTO
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int QuestionId { get; set; }
    }
}