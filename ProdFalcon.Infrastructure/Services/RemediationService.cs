using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Remediation;
using ProdFalcon.Domain.Enums;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Services;

public class RemediationService : IRemediationService
{
    private static readonly string[] SeverityOrder = ["Critical", "High", "Medium", "Low", "Info"];

    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemediationService> _logger;

    public RemediationService(
        ApplicationDbContext db,
        ITenantProvider tenant,
        IAuditService audit,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RemediationService> logger)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RemediationPlanDto> GetAsync(int scanResultId, CancellationToken cancellationToken = default)
    {
        await RequireScanAsync(scanResultId, cancellationToken);
        var rows = await _db.RemediationProposals.AsNoTracking()
            .Where(proposal => proposal.ScanResultId == scanResultId)
            .OrderBy(proposal => proposal.Id)
            .ToListAsync(cancellationToken);

        return Plan(scanResultId, rows, "Stored remediation proposals.");
    }

    public async Task<RemediationPlanDto> BuildAsync(int scanResultId, CancellationToken cancellationToken = default)
    {
        RejectViewers();
        var scan = await RequireScanAsync(scanResultId, cancellationToken);
        var existing = await _db.RemediationProposals
            .Where(proposal => proposal.ScanResultId == scanResultId)
            .ToListAsync(cancellationToken);
        var byIssue = existing.ToDictionary(proposal => proposal.ScanIssueId);

        var selected = scan.Issues
            .Where(issue => !string.Equals(issue.Severity, "Info", StringComparison.OrdinalIgnoreCase))
            .OrderBy(issue => Array.IndexOf(SeverityOrder, issue.Severity) is var rank && rank >= 0 ? rank : 9)
            .ThenBy(issue => issue.FilePath)
            .ThenBy(issue => issue.LineNumber)
            .Take(40)
            .ToList();

        foreach (var issue in selected)
        {
            var draft = RemediationComposer.Draft(issue);
            var enhanced = await TryEnhanceAsync(issue, draft, cancellationToken);
            if (byIssue.TryGetValue(issue.Id, out var proposal))
            {
                if (proposal.Status is "Approved" or "Rejected")
                    continue;

                Copy(proposal, issue, enhanced.Draft, enhanced.UsedModel);
            }
            else
            {
                proposal = new RemediationProposal { ScanResultId = scanResultId, ScanIssueId = issue.Id };
                Copy(proposal, issue, enhanced.Draft, enhanced.UsedModel);
                _db.RemediationProposals.Add(proposal);
                byIssue[issue.Id] = proposal;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var rows = byIssue.Values.OrderBy(proposal => proposal.Id).ToList();
        return Plan(scanResultId, rows, "Proposals are suggestions. Approving records a decision and does not modify the repository.");
    }

    public async Task<RemediationProposalDto> DecideAsync(int proposalId, string decision, string? note, CancellationToken cancellationToken = default)
    {
        RejectViewers();
        if (decision is not ("Approved" or "Rejected"))
            throw new ArgumentException("Decision must be Approved or Rejected.");

        var proposal = await _db.RemediationProposals.FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Remediation proposal was not found.");

        proposal.Status = decision;
        proposal.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        proposal.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(
            _tenant.TenantId,
            _tenant.UserId,
            "remediation." + decision.ToLowerInvariant(),
            $"proposal={proposal.Id};issue={proposal.ScanIssueId};file={proposal.FilePath}:{proposal.LineNumber}",
            cancellationToken);

        return ToDto(proposal);
    }

    private async Task<ScanResult> RequireScanAsync(int scanResultId, CancellationToken cancellationToken)
    {
        return await _db.ScanResults.Include(result => result.Issues)
                   .FirstOrDefaultAsync(result => result.Id == scanResultId, cancellationToken)
               ?? throw new KeyNotFoundException($"Scan result {scanResultId} was not found.");
    }

    private void RejectViewers()
    {
        if (_tenant.Role == TenantRole.Viewer)
            throw new UnauthorizedAccessException("Viewers can read proposals but cannot generate or approve them.");
    }

    private static void Copy(RemediationProposal proposal, ScanIssue issue, RemediationDraft draft, bool usedModel)
    {
        proposal.RuleId = issue.RuleId;
        proposal.Title = issue.Title;
        proposal.Severity = issue.Severity;
        proposal.FilePath = issue.FilePath;
        proposal.LineNumber = issue.LineNumber;
        proposal.ColumnNumber = issue.ColumnNumber;
        proposal.Explanation = draft.Explanation;
        proposal.SecureSuggestion = draft.SecureSuggestion;
        proposal.OriginalSnippet = draft.OriginalSnippet;
        proposal.PatchedSnippet = draft.PatchedSnippet;
        proposal.DiffText = draft.DiffText;
        proposal.Grounding = draft.Grounding;
        proposal.UsedExternalModel = usedModel;
        if (string.IsNullOrWhiteSpace(proposal.Status))
            proposal.Status = "Proposed";
    }

    private static RemediationPlanDto Plan(int scanResultId, IReadOnlyList<RemediationProposal> rows, string summary) =>
        new()
        {
            ScanResultId = scanResultId,
            Summary = rows.Count == 0
                ? "No remediation proposals are stored for this scan yet."
                : summary,
            Proposals = rows.Select(ToDto).ToList()
        };

    private static RemediationProposalDto ToDto(RemediationProposal proposal) =>
        new()
        {
            Id = proposal.Id,
            ScanResultId = proposal.ScanResultId,
            ScanIssueId = proposal.ScanIssueId,
            RuleId = proposal.RuleId,
            Title = proposal.Title,
            Severity = proposal.Severity,
            FilePath = proposal.FilePath,
            LineNumber = proposal.LineNumber,
            ColumnNumber = proposal.ColumnNumber,
            Explanation = proposal.Explanation,
            SecureSuggestion = proposal.SecureSuggestion,
            OriginalSnippet = proposal.OriginalSnippet,
            PatchedSnippet = proposal.PatchedSnippet,
            DiffText = proposal.DiffText,
            Status = proposal.Status,
            UsedExternalModel = proposal.UsedExternalModel,
            Grounding = proposal.Grounding,
            DecisionNote = proposal.DecisionNote
        };

    private async Task<(RemediationDraft Draft, bool UsedModel)> TryEnhanceAsync(
        ScanIssue issue,
        RemediationDraft draft,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return (draft, false);

        try
        {
            var client = _httpClientFactory.CreateClient("OpenAI");
            var prompt = $"""
                Explain this one stored finding and suggest a replacement snippet.
                Return JSON with keys explanation and patchedSnippet.
                Do not invent other files, vulnerabilities, or secret values.
                Finding: {issue.Title}
                Location: {issue.FilePath}:{issue.LineNumber}:{issue.ColumnNumber}
                Evidence: {Trim(issue.Evidence, 240)}
                Current remediation: {Trim(issue.Remediation, 300)}
                Secure example: {Trim(issue.SecureExample, 300)}
                """;

            var payload = JsonSerializer.Serialize(new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "You explain one stored static-analysis finding. JSON only. Do not claim the issue was exploited." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (draft, false);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (!TryReadEnhancement(text, out var explanation))
                return (draft, false);

            return (new RemediationDraft
            {
                Explanation = explanation,
                SecureSuggestion = draft.SecureSuggestion,
                OriginalSnippet = draft.OriginalSnippet,
                PatchedSnippet = draft.PatchedSnippet,
                DiffText = draft.DiffText,
                Grounding = "The explanation was rewritten by the configured model from the stored finding. The patch is the rule's secure example. The repository was not modified."
            }, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            _logger.LogWarning(ex, "Remediation model call failed. The local explanation was kept.");
            return (draft, false);
        }
    }

    private static bool TryReadEnhancement(string? text, out string explanation)
    {
        explanation = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var cleaned = text.Trim();
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        using var doc = JsonDocument.Parse(cleaned[start..(end + 1)]);
        explanation = doc.RootElement.TryGetProperty("explanation", out var explanationElement)
            ? explanationElement.GetString() ?? string.Empty
            : string.Empty;

        if (explanation.Length is < 20 or > 1200)
            return false;

        return !explanation.Contains("BEGIN PRIVATE", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
