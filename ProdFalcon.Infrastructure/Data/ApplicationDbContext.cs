using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Domain.Entities;
using ProdFalcon.Domain.Interfaces;
using ProdFalcon.Infrastructure.Tenancy;

namespace ProdFalcon.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Used by EF Core global query filters. Must be a property on the context instance.
    /// </summary>
    public Guid CurrentTenantId => _tenantProvider.TenantId;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ScanProject> ScanProjects => Set<ScanProject>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();
    public DbSet<ScanIssue> ScanIssues => Set<ScanIssue>();
    public DbSet<UserSubscription> Subscriptions => Set<UserSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<TenantMember>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.InviteToken);
            entity.HasOne(x => x.Tenant)
                .WithMany(t => t.Members)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Metadata).HasMaxLength(4000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Timestamp);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ScanProject>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(512);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.ZipPath).HasMaxLength(1024);
            entity.Property(x => x.ExtractedPath).HasMaxLength(1024);
            entity.HasIndex(x => x.UploadedAt);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
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
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ScanIssue>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Severity).HasMaxLength(32);
            entity.Property(x => x.RuleName).HasMaxLength(256);
            entity.Property(x => x.Category).HasMaxLength(128);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.RuleName);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StripeCustomerId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        ApplyTenantId();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantId()
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty)
            return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = tenantId;
        }
    }
}
