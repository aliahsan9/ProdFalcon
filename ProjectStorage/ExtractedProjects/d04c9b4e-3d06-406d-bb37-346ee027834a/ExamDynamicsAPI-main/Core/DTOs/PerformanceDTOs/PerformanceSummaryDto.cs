namespace ExamDynamicsAPI.Core.DTOs.PerformanceDTOs
{
    public class PerformanceSummaryDto
    {
        public int TotalExamsCompleted { get; set; }
        public double AveragePercentage { get; set; }
        public int BestScore { get; set; }
        public DateTime? LastCompletedAtUtc { get; set; }
    }
}
