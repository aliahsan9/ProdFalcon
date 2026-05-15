using ExamDynamicsAPI.Core.DTOs.ExamDTOs;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IExamService
    {
        Task<IEnumerable<ExamDto>> GetAllAsync();
        Task<ExamDto?> GetByIdAsync(int id);
        Task<ExamDto> CreateAsync(CreateExamDto createDto);
        Task<bool> UpdateAsync(int id, UpdateExamDto updateDto);
        Task<bool> DeleteAsync(int id);
    }
}
