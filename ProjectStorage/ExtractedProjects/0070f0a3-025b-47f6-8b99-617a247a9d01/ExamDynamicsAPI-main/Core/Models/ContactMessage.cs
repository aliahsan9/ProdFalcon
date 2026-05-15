namespace ExamDynamicsAPI.Core.Models
{
    public class ContactMessage
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
 