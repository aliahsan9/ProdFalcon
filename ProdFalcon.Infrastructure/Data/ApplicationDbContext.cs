using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Domain.Entities;

namespace ProdFalcon.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ScanProject> ScanProjects => Set<ScanProject>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();
    public DbSet<ScanIssue> ScanIssues => Set<ScanIssue>();
    public DbSet<UserSubscription> Subscriptions => Set<UserSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScanProject>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(512);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.ZipPath).HasMaxLength(1024);
            entity.Property(x => x.ExtractedPath).HasMaxLength(1024);
            entity.HasIndex(x => x.UploadedAt);
        });

        modelBuilder.Entity<ScanResult>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.ScanProject)
                .WithMany(p => p.Results)
                .HasForeignKey(x => x.ScanProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Issues)
                .WithOne(i => i.ScanResult)
                .HasForeignKey(i => i.ScanResultId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ScanProjectId);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ScanIssue>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Severity).HasMaxLength(32);
            entity.Property(x => x.RuleName).HasMaxLength(256);
            entity.Property(x => x.Category).HasMaxLength(128);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.RuleName);
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StripeCustomerId);
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }
}
