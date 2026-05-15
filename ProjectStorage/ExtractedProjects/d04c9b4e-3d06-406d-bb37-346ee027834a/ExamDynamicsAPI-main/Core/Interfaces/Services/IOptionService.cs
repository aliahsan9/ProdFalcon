using ExamDynamicsAPI.Core.DTOs.OptionDTOs;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IOptionService
    {
        Task<IEnumerable<OptionDto>> GetAllAsync();
        Task<OptionDto?> GetByIdAsync(int id);
        Task<OptionDto> CreateAsync(OptionCreateDto dto);
        Task<bool> UpdateAsync(int id, OptionUpdateDTO dto);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<OptionDto>> GetByQuestionIdAsync(int questionId);
    }
}
