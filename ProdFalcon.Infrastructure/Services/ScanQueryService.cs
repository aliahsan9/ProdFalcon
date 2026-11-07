using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Services;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class ScanQueryService : IScanQueryService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Open", "Detected", "Acknowledged", "InProgress", "Resolved", "AcceptedRisk"
    };

    private static readonly string[] QualityCategories = ["Maintainability", "Architecture", "Reliability", "Quality", "Performance"];

    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditService _auditService;

    public ScanQueryService(ApplicationDbContext db, ITenantProvider tenantProvider, IAuditService auditService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _auditService = auditService;
    }

    public async Task<PagedResult<ScanHistoryItemDto>> GetHistoryAsync(Guid? projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ScanResults.AsNoTracking().AsQueryable();
        if (projectId.HasValue)
            query = query.Where(r => r.ScanProjectId == projectId.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ScanHistoryItemDto
            {
                ScanResultId = r.Id,
                ProjectId = r.ScanProjectId,
                ProjectName = r.ScanProject!.DisplayName ?? r.ScanProject.FileName,
                SourceType = r.ScanProject.SourceType,
                RepositoryUrl = r.ScanProject.RepositoryUrl,
                Branch = r.ScanProject.Branch,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                Stage = r.Stage,
                StageMessage = r.StageMessage,
                Score = r.Score,
                RiskLevel = r.RiskLevel,
                DurationMs = r.DurationMs,
                IssueCount = r.Issues.Count,
                PreviousScore = r.PreviousScore
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ScanHistoryItemDto> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }

    public async Task<PagedResult<ScanIssueSummaryDto>> GetFindingsAsync(FindingQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var findings = _db.ScanIssues.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Severity))
            findings = findings.Where(i => i.Severity == query.Severity);
        if (!string.IsNullOrWhiteSpace(query.Status))
            findings = findings.Where(i => i.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Category))
            findings = findings.Where(i => i.Category == query.Category);
        if (string.Equals(query.Scope, "quality", StringComparison.OrdinalIgnoreCase))
        {
            findings = findings.Where(i =>
                QualityCategories.Contains(i.Category) ||
                i.RuleId.StartsWith("QUAL-") ||
                i.RuleId.StartsWith("REL-") ||
                i.RuleId.StartsWith("PERF-"));
        }
        else if (string.Equals(query.Scope, "security", StringComparison.OrdinalIgnoreCase))
        {
            findings = findings.Where(i =>
                !QualityCategories.Contains(i.Category) &&
                !i.RuleId.StartsWith("QUAL-") &&
                !i.RuleId.StartsWith("REL-") &&
                !i.RuleId.StartsWith("PERF-"));
        }
        if (query.ProjectId.HasValue)
            findings = findings.Where(i => i.ScanResult!.ScanProjectId == query.ProjectId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            findings = findings.Where(i =>
                i.Title.Contains(term) ||
                i.FilePath.Contains(term) ||
                i.RuleName.Contains(term) ||
                i.Category.Contains(term));
        }

        var total = await findings.CountAsync(cancellationToken);
        var rows = await findings
            .OrderByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ScanIssueSummaryDto>
        {
            Items = rows.Select(ScanMappings.ToIssue).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<ScanComparisonDto> CompareAsync(int baselineId, int currentId, CancellationToken cancellationToken = default)
    {
        var baseline = await _db.ScanResults.AsNoTracking().Include(r => r.Issues).FirstOrDefaultAsync(r => r.Id == baselineId, cancellationToken)
            ?? throw new KeyNotFoundException("Baseline scan was not found.");
        var current = await _db.ScanResults.AsNoTracking().Include(r => r.Issues).FirstOrDefaultAsync(r => r.Id == currentId, cancellationToken)
            ?? throw new KeyNotFoundException("Current scan was not found.");

        var baselineMap = baseline.Issues
            .Where(i => !string.IsNullOrWhiteSpace(i.Fingerprint))
            .GroupBy(i => i.Fingerprint)
            .ToDictionary(g => g.Key, g => ScanMappings.ToIssue(g.First()));
        var currentMap = current.Issues
            .Where(i => !string.IsNullOrWhiteSpace(i.Fingerprint))
            .GroupBy(i => i.Fingerprint)
            .ToDictionary(g => g.Key, g => ScanMappings.ToIssue(g.First()));

        return new ScanComparisonDto
        {
            BaselineId = baselineId,
            CurrentId = currentId,
            BaselineScore = baseline.Score,
            CurrentScore = current.Score,
            ScoreChange = current.Score - baseline.Score,
            Introduced = currentMap.Where(pair => !baselineMap.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList(),
            Resolved = baselineMap.Where(pair => !currentMap.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList(),
            Unchanged = currentMap.Where(pair => baselineMap.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList()
        };
    }

    public async Task<ScanIssueSummaryDto> UpdateFindingStatusAsync(int issueId, string status, string? notes, CancellationToken cancellationToken = default)
    {
        if (string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Verified is recorded only when a later scan no longer reports a finding that was marked Resolved.");

        if (!AllowedStatuses.Contains(status))
            throw new ArgumentException("Status must be Open, Detected, Acknowledged, InProgress, Resolved, or AcceptedRisk.");

        var issue = await _db.ScanIssues.Include(i => i.ScanResult).FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken)
            ?? throw new KeyNotFoundException("Finding was not found.");

        var from = string.IsNullOrWhiteSpace(issue.Status) ? "Open" : issue.Status;
        issue.Status = status;
        if (issue.ScanResult != null && !string.IsNullOrWhiteSpace(issue.Fingerprint))
        {
            var tracked = await _db.TrackedFindings.FirstOrDefaultAsync(
                finding => finding.ScanProjectId == issue.ScanResult.ScanProjectId && finding.Fingerprint == issue.Fingerprint,
                cancellationToken);
            if (tracked == null)
            {
                tracked = new TrackedFinding
                {
                    ScanProjectId = issue.ScanResult.ScanProjectId,
                    Fingerprint = issue.Fingerprint,
                    RuleId = issue.RuleId,
                    Title = issue.Title,
                    FilePath = issue.FilePath,
                    Severity = issue.Severity,
                    FirstScanResultId = issue.ScanResultId,
                    LastScanResultId = issue.ScanResultId,
                    FirstSeenAt = DateTime.UtcNow,
                    OccurrenceCount = 1
                };
                _db.TrackedFindings.Add(tracked);
            }

            tracked.Status = status;
            tracked.LastSeenAt = DateTime.UtcNow;
            tracked.ResolvedAt = string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : tracked.ResolvedAt;
            tracked.Notes = notes;
        }
        if (!string.IsNullOrWhiteSpace(notes))
            issue.Notes = notes.Length > 2000 ? notes[..2000] : notes;

        _db.FindingStatusHistory.Add(new FindingStatusHistory
        {
            ScanIssueId = issue.Id,
            FromStatus = from,
            ToStatus = status,
            Notes = issue.Notes,
            UserId = _tenantProvider.UserId,
            ChangedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            _tenantProvider.TenantId,
            _tenantProvider.UserId,
            "FindingStatusChanged",
            $"{{\"issueId\":{issue.Id},\"from\":\"{from}\",\"to\":\"{status}\"}}",
            cancellationToken);

        return ScanMappings.ToIssue(issue);
    }

    public async Task<ScanResultDto?> GetScanAsync(int scanResultId, CancellationToken cancellationToken = default)
    {
        var result = await _db.ScanResults
            .Include(r => r.Issues)
            .Include(r => r.ScanProject)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == scanResultId, cancellationToken);

        return result == null ? null : ScanMappings.ToDto(result, result.ScanProject);
    }
}
