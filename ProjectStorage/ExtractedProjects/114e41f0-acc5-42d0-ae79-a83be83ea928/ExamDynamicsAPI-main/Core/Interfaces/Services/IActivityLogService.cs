namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(int userId, string activityType, string? description = null, string? metadataJson = null);
    }
}
