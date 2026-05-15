using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ExamDynamicsAPI.Core.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(200)]
        public string? Institution { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        public virtual ICollection<Answer> Answers { get; set; } = new HashSet<Answer>();
        public virtual ICollection<ExamAttempt> ExamAttempts { get; set; } = new HashSet<ExamAttempt>();
        public virtual ICollection<UserActivity> Activities { get; set; } = new HashSet<UserActivity>();
    }

    public class ApplicationRole : IdentityRole<int>
    {
        public string Description { get; set; } = string.Empty;
        public virtual ICollection<IdentityUserRole<int>> UserRoles { get; set; } = new HashSet<IdentityUserRole<int>>();
    }
}
