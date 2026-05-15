namespace ExamDynamicsAPI.Core.DTOs.PerformanceDTOs
{
    public class ExamAttemptResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public double Percentage { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public string CertificateCode { get; set; } = string.Empty;
    }
}
