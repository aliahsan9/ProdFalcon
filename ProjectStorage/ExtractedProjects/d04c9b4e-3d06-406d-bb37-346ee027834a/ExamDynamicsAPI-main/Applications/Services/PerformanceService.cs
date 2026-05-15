using ExamDynamicsAPI.Core.DTOs.PerformanceDTOs;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExamDynamicsAPI.Applications.Services
{
    public class PerformanceService : IPerformanceService
    {
        private readonly ExamDynamicsDbContext _db;
        private readonly IActivityLogService _activityLog;

        public PerformanceService(ExamDynamicsDbContext db, IActivityLogService activityLog)
        {
            _db = db;
            _activityLog = activityLog;
        }

        private static int? GetUserId(ClaimsPrincipal user)
        {
            var s = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("UserId");
            return int.TryParse(s, out var id) ? id : null;
        }

        public async Task<ExamAttemptResponseDto> RecordExamAttemptAsync(ClaimsPrincipal principal, SubmitExamAttemptDto dto)
        {
            var userId = GetUserId(principal) ?? throw new UnauthorizedAccessException();
            var examExists = await _db.Exams.AnyAsync(e => e.ExamId == dto.ExamId);
            if (!examExists)
                throw new KeyNotFoundException("Exam not found.");

            var total = dto.TotalQuestions;
            var pct = total > 0 ? Math.Round(dto.Score * 100.0 / total, 2) : 0;
            var code = ("ED-" + Guid.NewGuid().ToString("N")[..11]).ToUpperInvariant();

            var attempt = new ExamAttempt
            {
                UserId = userId,
                ExamId = dto.ExamId,
                ExamTitle = dto.ExamTitle.Trim(),
                Score = dto.Score,
                TotalQuestions = total,
                Percentage = pct,
                CompletedAtUtc = DateTime.UtcNow,
                CertificateCode = code
            };

            _db.ExamAttempts.Add(attempt);
            await _db.SaveChangesAsync();

            await _activityLog.LogAsync(
                userId,
                "ExamCompleted",
                $"Completed \"{attempt.ExamTitle}\" — {attempt.Score}/{attempt.TotalQuestions} ({pct}%)",
                System.Text.Json.JsonSerializer.Serialize(new { attempt.ExamId, attempt.Percentage }));

            return MapAttempt(attempt);
        }

        public async Task<IReadOnlyList<ExamAttemptResponseDto>> GetRecentAttemptsAsync(ClaimsPrincipal user, int take = 50)
        {
            var userId = GetUserId(user) ?? throw new UnauthorizedAccessException();
            var list = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CompletedAtUtc)
                .Take(take)
                .ToListAsync();
            return list.Select(MapAttempt).ToList();
        }

        public async Task<PerformanceSummaryDto> GetSummaryAsync(ClaimsPrincipal user)
        {
            var userId = GetUserId(user) ?? throw new UnauthorizedAccessException();
            var attempts = await _db.ExamAttempts.AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToListAsync();

            if (attempts.Count == 0)
            {
                return new PerformanceSummaryDto();
            }

            return new PerformanceSummaryDto
            {
                TotalExamsCompleted = attempts.Count,
                AveragePercentage = Math.Round(attempts.Average(a => a.Percentage), 2),
                BestScore = attempts.Max(a => a.Score),
                LastCompletedAtUtc = attempts.Max(a => a.CompletedAtUtc)
            };
        }

        public async Task<IReadOnlyList<PerformanceChartPointDto>> GetChartSeriesAsync(ClaimsPrincipal user, int days = 30)
        {
            var userId = GetUserId(user) ?? throw new UnauthorizedAccessException();
            var from = DateTime.UtcNow.Date.AddDays(-days + 1);

            var raw = await _db.ExamAttempts.AsNoTracking()
                .Where(a => a.UserId == userId && a.CompletedAtUtc >= from)
                .ToListAsync();

            var grouped = raw
                .GroupBy(a => a.CompletedAtUtc.Date)
                .OrderBy(g => g.Key)
                .Select(g => new PerformanceChartPointDto
                {
                    IsoDate = g.Key.ToString("yyyy-MM-dd"),
                    DateLabel = g.Key.ToString("MMM d"),
                    AveragePercentage = Math.Round(g.Average(a => a.Percentage), 2),
                    AttemptCount = g.Count()
                })
                .ToList();

            return grouped;
        }

        public async Task<IReadOnlyList<UserActivityItemDto>> GetRecentActivityAsync(ClaimsPrincipal user, int take = 30)
        {
            var userId = GetUserId(user) ?? throw new UnauthorizedAccessException();
            return await _db.UserActivities.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(take)
                .Select(a => new UserActivityItemDto
                {
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    CreatedAtUtc = a.CreatedAtUtc
                })
                .ToListAsync();
        }

        public async Task<CertificateDto?> GetCertificateAsync(ClaimsPrincipal user, int attemptId)
        {
            var userId = GetUserId(user) ?? throw new UnauthorizedAccessException();
            var attempt = await _db.ExamAttempts.AsNoTracking()
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return null;

            return new CertificateDto
            {
                StudentName = attempt.User.FullName,
                ExamTitle = attempt.ExamTitle,
                Score = attempt.Score,
                TotalQuestions = attempt.TotalQuestions,
                Percentage = attempt.Percentage,
                CompletedAtUtc = attempt.CompletedAtUtc,
                CertificateCode = attempt.CertificateCode
            };
        }

        private static ExamAttemptResponseDto MapAttempt(ExamAttempt a) => new()
        {
            Id = a.Id,
            ExamId = a.ExamId,
            ExamTitle = a.ExamTitle,
            Score = a.Score,
            TotalQuestions = a.TotalQuestions,
            Percentage = a.Percentage,
            CompletedAtUtc = a.CompletedAtUtc,
            CertificateCode = a.CertificateCode
        };
    }
}
