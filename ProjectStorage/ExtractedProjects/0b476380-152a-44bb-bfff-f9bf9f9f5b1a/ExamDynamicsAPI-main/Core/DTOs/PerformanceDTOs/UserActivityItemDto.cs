namespace ExamDynamicsAPI.Core.DTOs.PerformanceDTOs
{
    public class UserActivityItemDto
    {
        public string ActivityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
