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

    public DbSet<ScanSession> ScanSessions { get; set; }

    public DbSet<ScanIssue> ScanIssues => Set<ScanIssue>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScanSession>()
            .HasMany(s => s.Issues)
            .WithOne(i => i.ScanSession)
            .HasForeignKey(i => i.ScanSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ScanResult>()
        .HasKey(x => x.Id);
    }
}