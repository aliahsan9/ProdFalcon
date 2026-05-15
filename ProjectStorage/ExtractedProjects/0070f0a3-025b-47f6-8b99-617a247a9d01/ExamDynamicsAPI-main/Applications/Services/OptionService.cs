using AutoMapper;
using ExamDynamicsAPI.Core.DTOs.OptionDTOs;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Applications.Services
{
    public class OptionService : IOptionService
    {
        private readonly IOptionRepository _repository;
        private readonly IMapper _mapper;

        public OptionService(IOptionRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<OptionDto>> GetAllAsync()
        {
            var entities = await _repository.GetAllAsync();
            return _mapper.Map<IEnumerable<OptionDto>>(entities);
        }

        public async Task<OptionDto?> GetByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            return _mapper.Map<OptionDto?>(entity);
        }

        public async Task<OptionDto> CreateAsync(OptionCreateDto dto)
        {
            var entity = _mapper.Map<Option>(dto);
            await _repository.AddAsync(entity);
            return _mapper.Map<OptionDto>(entity);
        }

        public async Task<bool> UpdateAsync(int id, OptionUpdateDTO dto)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return false;

            _mapper.Map(dto, entity);
            await _repository.UpdateAsync(entity);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return false;

            await _repository.DeleteAsync(entity);
            return true;
        }

        public async Task<IEnumerable<OptionDto>> GetByQuestionIdAsync(int questionId)
        {
            var entities = await _repository.GetByQuestionIdAsync(questionId);
            return _mapper.Map<IEnumerable<OptionDto>>(entities);
        }
    }
}
