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
    public DbSet<FindingStatusHistory> FindingStatusHistory => Set<FindingStatusHistory>();
    public DbSet<TrackedFinding> TrackedFindings => Set<TrackedFinding>();
    public DbSet<CopilotMessage> CopilotMessages => Set<CopilotMessage>();
    public DbSet<RepositoryRegistration> RepositoryRegistrations => Set<RepositoryRegistration>();
    public DbSet<RemediationProposal> RemediationProposals => Set<RemediationProposal>();
    public DbSet<GitHubAccountConnection> GitHubAccountConnections => Set<GitHubAccountConnection>();
    public DbSet<SecurityPolicy> SecurityPolicies => Set<SecurityPolicy>();
    public DbSet<PullRequestScan> PullRequestScans => Set<PullRequestScan>();
    public DbSet<CollaborationNote> CollaborationNotes => Set<CollaborationNote>();

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
            entity.Property(x => x.SourceType).HasMaxLength(32);
            entity.Property(x => x.RepositoryUrl).HasMaxLength(500);
            entity.Property(x => x.Branch).HasMaxLength(200);
            entity.Property(x => x.CommitSha).HasMaxLength(64);
            entity.Property(x => x.DisplayName).HasMaxLength(256);
            entity.Property(x => x.DetectedTechnologies).HasMaxLength(2000);
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

            entity.Property(x => x.Stage).HasMaxLength(64);
            entity.Property(x => x.StageMessage).HasMaxLength(500);
            entity.Property(x => x.Technologies).HasMaxLength(2000);
            entity.Property(x => x.RiskLevel).HasMaxLength(32);
            entity.Property(x => x.ConfigurationJson).HasMaxLength(2000);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.CommitSha).HasMaxLength(64);
            entity.HasIndex(x => x.ScanProjectId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ScanIssue>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Severity).HasMaxLength(32);
            entity.Property(x => x.RuleName).HasMaxLength(256);
            entity.Property(x => x.Category).HasMaxLength(128);
            entity.Property(x => x.Fingerprint).HasMaxLength(128);
            entity.Property(x => x.Confidence).HasMaxLength(16);
            entity.Property(x => x.Cwe).HasMaxLength(32);
            entity.Property(x => x.Cve).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.DetectionMethod).HasMaxLength(64);
            entity.Property(x => x.Evidence).HasMaxLength(2000);
            entity.Property(x => x.Impact).HasMaxLength(2000);
            entity.Property(x => x.Remediation).HasMaxLength(4000);
            entity.Property(x => x.SecureExample).HasMaxLength(4000);
            entity.Property(x => x.References).HasMaxLength(2000);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.FilePath).HasMaxLength(1024);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.RuleName);
            entity.HasIndex(x => x.Fingerprint);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<FindingStatusHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStatus).HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasMaxLength(32);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => x.ScanIssueId);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TrackedFinding>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Fingerprint).HasMaxLength(128);
            entity.Property(x => x.RuleId).HasMaxLength(128);
            entity.Property(x => x.Title).HasMaxLength(500);
            entity.Property(x => x.FilePath).HasMaxLength(1024);
            entity.Property(x => x.Severity).HasMaxLength(32);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ScanProjectId, x.Fingerprint }).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Status);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<CopilotMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(16);
            entity.Property(x => x.Content).HasMaxLength(8000);
            entity.HasIndex(x => x.ScanResultId);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<RemediationProposal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleId).HasMaxLength(128);
            entity.Property(x => x.Title).HasMaxLength(500);
            entity.Property(x => x.Severity).HasMaxLength(32);
            entity.Property(x => x.FilePath).HasMaxLength(1024);
            entity.Property(x => x.Explanation).HasMaxLength(4000);
            entity.Property(x => x.SecureSuggestion).HasMaxLength(4000);
            entity.Property(x => x.OriginalSnippet).HasMaxLength(2000);
            entity.Property(x => x.PatchedSnippet).HasMaxLength(4000);
            entity.Property(x => x.DiffText).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Grounding).HasMaxLength(500);
            entity.Property(x => x.DecisionNote).HasMaxLength(500);
            entity.HasIndex(x => new { x.ScanResultId, x.ScanIssueId }).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.ScanResult)
                .WithMany()
                .HasForeignKey(x => x.ScanResultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<RepositoryRegistration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Owner).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Branch).HasMaxLength(200);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.TokenPrefix).HasMaxLength(16);
            entity.HasIndex(x => new { x.TenantId, x.Owner, x.Name }).IsUnique();
            entity.HasIndex(x => x.TokenHash);
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<GitHubAccountConnection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GitHubLogin).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProtectedAccessToken).IsRequired();
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
            entity.Property(x => x.Scopes).HasMaxLength(400);
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<SecurityPolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PullRequestScan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Owner).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.HeadSha).HasMaxLength(64);
            entity.Property(x => x.Branch).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(2000);
            entity.Property(x => x.Source).HasMaxLength(32);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<CollaborationNote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AuthorName).HasMaxLength(200);
            entity.Property(x => x.Body).HasMaxLength(2000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ScanResultId);
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
