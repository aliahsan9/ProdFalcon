namespace ExamDynamicsAPI.Core.Interfaces.Repositories
{
    public interface IExamDynamicsUnitOfWork : IDisposable
    {

        // ================= Exams & Content =================

        IExamRepository Exams { get; }
        IQuestionRepository Questions { get; }
        IOptionRepository Options { get; }
     
        // ================= Save Changes =================
        Task<int> CompleteAsync();
    }
}
