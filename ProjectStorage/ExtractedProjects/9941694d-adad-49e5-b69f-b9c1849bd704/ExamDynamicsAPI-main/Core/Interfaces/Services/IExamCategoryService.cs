using ExamDynamicsAPI.Core.DTOs.ExamCategoryDTOs;

namespace ExamDynamicsAPI.Core.Interfaces.Services

{
    public interface IExamCategoryService
    {
        Task<IEnumerable<ExamCategoryDto>> GetAllCategoriesAsync();
        Task<ExamCategoryDto> GetByIdAsync(int id);
        Task CreateCategoryAsync(ExamCategoryDto categoryDto);
        Task UpdateCategoryAsync(ExamCategoryDto categoryDto);
        Task DeleteCategoryAsync(int id);
    }
}
