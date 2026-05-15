namespace ExamDynamicsAPI.Core.DTOs.PerformanceDTOs
{
    public class PerformanceChartPointDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public string IsoDate { get; set; } = string.Empty;
        public double AveragePercentage { get; set; }
        public int AttemptCount { get; set; }
    }
}
