using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Core.Interfaces.Repositories
{
    public interface IExamCategoryRepository
    {
        Task<IEnumerable<ExamCategory>> GetAllAsync();
        Task<ExamCategory?> GetByIdAsync(int id);
        Task AddAsync(ExamCategory category);
        Task UpdateAsync(ExamCategory category);
        Task DeleteAsync(int id);
    }
}
