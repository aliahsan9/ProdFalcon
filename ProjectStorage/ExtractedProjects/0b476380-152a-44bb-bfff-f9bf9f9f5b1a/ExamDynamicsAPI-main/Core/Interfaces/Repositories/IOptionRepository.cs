using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Core.Interfaces.Repositories
{
    public interface IOptionRepository
    {
        Task<IEnumerable<Option>> GetAllAsync();
        Task<Option?> GetByIdAsync(int id);
        Task AddAsync(Option option);
        Task UpdateAsync(Option option);
        Task DeleteAsync(Option option);
        Task<IEnumerable<Option>> GetByQuestionIdAsync(int questionId);
    }
}
