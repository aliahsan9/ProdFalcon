using ExamDynamicsAPI.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExamDynamicsAPI.Infrastructure.Data
{

    public class ExamDynamicsDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public ExamDynamicsDbContext(DbContextOptions<ExamDynamicsDbContext> options)
            : base(options)
        {
        }

         public DbSet<ContactMessage> ContactMessages { get; set; }


        // Exams & Subjects
        public DbSet<Exam> Exams { get; set; } = null!;

        // Questions & Options
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<Option> Options { get; set; } = null!;
        // Answers & Quizzes
        public DbSet<Answer> Answers { get; set; } = null!;
        public DbSet<ExamCategory> ExamCategories { get; set; } = null!;
        public DbSet<ExamAttempt> ExamAttempts { get; set; } = null!;
        public DbSet<UserActivity> UserActivities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // IMPORTANT: Identity setup
            base.OnModelCreating(modelBuilder);

                // ===== Question - Option (1:Many) =====
            modelBuilder.Entity<Question>()
                .HasMany(q => q.Options)
                .WithOne(o => o.Question)
                .HasForeignKey(o => o.QuestionId);
              

            // ===== Answer - User & Question =====
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.User)
                .WithMany(u => u.Answers)
                .HasForeignKey(a => a.UserId);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId);

            // ===== Option - Answer (1:Many) =====
            modelBuilder.Entity<Option>()
                .HasMany(o => o.Answers)
                .WithOne(a => a.Option)
                .HasForeignKey(a => a.OptionId);

            modelBuilder.Entity<ExamAttempt>()
                .HasOne(ea => ea.User)
                .WithMany(u => u.ExamAttempts)
                .HasForeignKey(ea => ea.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExamAttempt>()
                .HasOne(ea => ea.Exam)
                .WithMany(e => e.Attempts)
                .HasForeignKey(ea => ea.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamAttempt>()
                .HasIndex(ea => ea.CertificateCode)
                .IsUnique();

            modelBuilder.Entity<UserActivity>()
                .HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserActivity>()
                .HasIndex(a => new { a.UserId, a.CreatedAtUtc });
        }
    }
}
