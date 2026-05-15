using ExamDynamicsAPI.Core.DTOs.PerformanceDTOs;
using System.Security.Claims;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IPerformanceService
    {
        Task<ExamAttemptResponseDto> RecordExamAttemptAsync(ClaimsPrincipal user, SubmitExamAttemptDto dto);
        Task<IReadOnlyList<ExamAttemptResponseDto>> GetRecentAttemptsAsync(ClaimsPrincipal user, int take = 50);
        Task<PerformanceSummaryDto> GetSummaryAsync(ClaimsPrincipal user);
        Task<IReadOnlyList<PerformanceChartPointDto>> GetChartSeriesAsync(ClaimsPrincipal user, int days = 30);
        Task<IReadOnlyList<UserActivityItemDto>> GetRecentActivityAsync(ClaimsPrincipal user, int take = 30);
        Task<CertificateDto?> GetCertificateAsync(ClaimsPrincipal user, int attemptId);
    }
}
