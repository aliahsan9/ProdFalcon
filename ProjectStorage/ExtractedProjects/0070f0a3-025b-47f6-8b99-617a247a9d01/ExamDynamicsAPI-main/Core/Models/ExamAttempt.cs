using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamDynamicsAPI.Core.Models
{
    public class ExamAttempt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int ExamId { get; set; }

        [ForeignKey(nameof(ExamId))]
        public Exam Exam { get; set; } = null!;

        [MaxLength(200)]
        public string ExamTitle { get; set; } = string.Empty;

        public int Score { get; set; }

        public int TotalQuestions { get; set; }

        public double Percentage { get; set; }

        public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Human-readable verification code printed on the certificate.</summary>
        [MaxLength(32)]
        public string CertificateCode { get; set; } = string.Empty;
    }
}
