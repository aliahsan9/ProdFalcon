namespace ExamDynamicsAPI.Core.DTOs.ProfileDTOs
{
    public class StudentProfileDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Institution { get; set; }
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
