using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;

namespace ExamDynamicsAPI.Applications.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly ExamDynamicsDbContext _db;

        public ActivityLogService(ExamDynamicsDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(int userId, string activityType, string? description = null, string? metadataJson = null)
        {
            _db.UserActivities.Add(new UserActivity
            {
                UserId = userId,
                ActivityType = activityType,
                Description = description,
                MetadataJson = metadataJson,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
