namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IAIService
{
    Task<string> GetAnswerAsync(string question, string context);
}
}