namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IRagService
{
    Task<string> GetRelevantContextAsync(string query);
}
}